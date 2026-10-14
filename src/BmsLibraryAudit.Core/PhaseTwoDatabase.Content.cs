namespace BmsLibraryAudit.Core;

internal sealed partial class PhaseTwoDatabase
{
    private void InitializeContent()
    {
        Execute(ContentSchema);
        foreach (var (table, keys) in new[] {
            ("content_hashes", "physical_file_id"), ("content_component_outcomes", "component_id"),
            ("comparisons", "component_id,root_a_id,root_b_id"),
            ("comparison_entries", "component_id,root_a_id,root_b_id,relative_path_key"),
            ("comparison_content_groups", "component_id,root_a_id,root_b_id,projection,sha256") })
        {
            foreach (var operation in new[] { "INSERT", "UPDATE", "DELETE" })
            {
                var row = operation == "INSERT" ? "NEW" : "OLD";
                var predicate = $"COALESCE((SELECT state FROM sessions WHERE session_id={row}.content_session_id),'')<>'RUNNING' OR COALESCE((SELECT stage FROM sessions WHERE session_id={row}.content_session_id),'')<>'CONTENT'";
                if (operation == "UPDATE")
                    predicate += " OR " + string.Join(" OR ", ("content_session_id,preflight_id," + keys).Split(',').Select(k => $"NEW.{k} IS NOT OLD.{k}"));
                Execute($"CREATE TRIGGER seal_{table}_{operation} BEFORE {operation} ON {table} WHEN {predicate} BEGIN SELECT RAISE(ABORT,'Content evidence identity/terminal seal is immutable'); END");
            }
        }
        foreach (var operation in new[] { "INSERT", "UPDATE" })
        {
            Execute($"""
                CREATE TRIGGER content_hash_scope_{operation} BEFORE {operation} ON content_hashes
                WHEN NOT EXISTS(SELECT 1 FROM physical_files p JOIN observation_memberships m ON m.preflight_id=p.preflight_id AND m.entry_id=p.entry_id
                    JOIN component_memberships cm ON cm.preflight_id=m.preflight_id AND cm.observation_root_id=m.observation_root_id
                    JOIN candidate_components c ON c.preflight_id=cm.preflight_id AND c.component_id=cm.component_id
                    WHERE p.preflight_id=NEW.preflight_id AND p.physical_file_id=NEW.physical_file_id AND c.outcome='ADMITTED')
                BEGIN SELECT RAISE(ABORT,'Content hash outside admitted union'); END;
                CREATE TRIGGER content_stamp_{operation} BEFORE {operation} ON content_hashes WHEN NEW.status<>'PENDING' AND NOT EXISTS(
                    SELECT 1 FROM physical_files p JOIN preflight_entries e ON e.preflight_id=p.preflight_id AND e.entry_id=p.entry_id
                    WHERE p.preflight_id=NEW.preflight_id AND p.physical_file_id=NEW.physical_file_id
                    AND NEW.before_size IS e.size AND NEW.after_size IS e.size
                    AND NEW.before_mtime_ticks IS e.mtime_ticks AND NEW.after_mtime_ticks IS e.mtime_ticks
                    AND NEW.before_native_identity IS e.native_identity AND NEW.after_native_identity IS e.native_identity
                    AND (NEW.status<>'SUCCESS' OR NEW.verified_size IS e.size))
                BEGIN SELECT RAISE(ABORT,'Content stamps differ from accepted inventory'); END;
                CREATE TRIGGER content_outcome_scope_{operation} BEFORE {operation} ON content_component_outcomes
                WHEN COALESCE((SELECT outcome FROM candidate_components WHERE preflight_id=NEW.preflight_id AND component_id=NEW.component_id),'')<>'ADMITTED'
                BEGIN SELECT RAISE(ABORT,'Content outcome outside admitted component'); END;
                CREATE TRIGGER content_outcome_counts_{operation} BEFORE {operation} ON content_component_outcomes WHEN NOT EXISTS(
                    SELECT count(*) FROM component_memberships cm JOIN observation_memberships m ON m.preflight_id=cm.preflight_id AND m.observation_root_id=cm.observation_root_id
                    JOIN physical_files p ON p.preflight_id=m.preflight_id AND p.entry_id=m.entry_id
                    LEFT JOIN content_hashes h ON h.content_session_id=NEW.content_session_id AND h.preflight_id=p.preflight_id AND h.physical_file_id=p.physical_file_id
                    WHERE cm.preflight_id=NEW.preflight_id AND cm.component_id=NEW.component_id
                    HAVING count(DISTINCT p.physical_file_id)=NEW.required_physical_file_count
                      AND count(DISTINCT CASE WHEN h.status='SUCCESS' THEN p.physical_file_id END)=NEW.trusted_successful_hash_count
                      AND count(DISTINCT CASE WHEN h.status IN ('ERROR','UNSTABLE') THEN p.physical_file_id END)=NEW.failed_untrusted_file_count
                      AND count(DISTINCT CASE WHEN h.status IN ('ERROR','UNSTABLE') THEN cm.observation_root_id END)=NEW.affected_observation_root_count)
                BEGIN SELECT RAISE(ABORT,'Content outcome counts differ from dependencies'); END;
                CREATE TRIGGER comparison_complete_{operation} BEFORE {operation} ON comparisons
                WHEN NOT EXISTS(SELECT 1 FROM content_component_outcomes WHERE content_session_id=NEW.content_session_id AND component_id=NEW.component_id
                    AND outcome='COMPLETE' AND comparison_eligible=1 AND coherent=1 AND sealed=1)
                BEGIN SELECT RAISE(ABORT,'Comparisons require a complete component'); END;
                CREATE TRIGGER diagnostic_scope_{operation} BEFORE {operation} ON diagnostics
                WHEN NEW.preflight_id IS NOT (SELECT COALESCE(parent_preflight_id,session_id) FROM sessions WHERE session_id=NEW.session_id)
                  OR NEW.gate IS NOT (SELECT stage FROM sessions WHERE session_id=NEW.session_id)
                  OR (NEW.gate='CONTENT' AND NEW.component_id IS NOT NULL AND COALESCE((SELECT outcome FROM candidate_components WHERE preflight_id=NEW.preflight_id AND component_id=NEW.component_id),'')<>'ADMITTED')
                  OR (NEW.physical_file_id IS NOT NULL AND NEW.entry_id IS NOT (SELECT entry_id FROM physical_files WHERE preflight_id=NEW.preflight_id AND physical_file_id=NEW.physical_file_id))
                BEGIN SELECT RAISE(ABORT,'Diagnostic session/preflight scope mismatch'); END;
                """);
        }
        Execute("""
            CREATE TRIGGER content_hashes_derived_INSERT BEFORE INSERT ON content_hashes WHEN
                (SELECT state FROM sessions WHERE session_id=NEW.content_session_id)='RUNNING' AND EXISTS(SELECT 1 FROM content_component_outcomes WHERE content_session_id=NEW.content_session_id)
            BEGIN SELECT RAISE(ABORT,'Hash population already used by component outcomes'); END;
            CREATE TRIGGER content_hashes_derived_UPDATE BEFORE UPDATE ON content_hashes WHEN
                (SELECT state FROM sessions WHERE session_id=OLD.content_session_id)='RUNNING' AND EXISTS(SELECT 1 FROM content_component_outcomes WHERE content_session_id=OLD.content_session_id)
            BEGIN SELECT RAISE(ABORT,'Hash population already used by component outcomes'); END;
            CREATE TRIGGER content_hashes_derived_DELETE BEFORE DELETE ON content_hashes WHEN
                (SELECT state FROM sessions WHERE session_id=OLD.content_session_id)='RUNNING' AND EXISTS(SELECT 1 FROM content_component_outcomes WHERE content_session_id=OLD.content_session_id)
            BEGIN SELECT RAISE(ABORT,'Hash population already used by component outcomes'); END;
            CREATE TRIGGER content_parent BEFORE INSERT ON sessions WHEN NEW.stage='CONTENT' AND NOT EXISTS(
                SELECT 1 FROM sessions p WHERE p.session_id=NEW.parent_preflight_id AND p.stage='PREFLIGHT' AND p.state='COMPLETED'
                AND p.final_evidence_digest=NEW.accepted_manifest_sha256 AND length(NEW.accepted_manifest_sha256)=64
                AND NEW.accepted_manifest_sha256 NOT GLOB '*[^0-9a-f]*' AND NEW.source_component_count=p.source_component_count
                AND EXISTS(SELECT 1 FROM candidate_components c WHERE c.preflight_id=p.session_id AND c.outcome='ADMITTED'))
            BEGIN SELECT RAISE(ABORT,'CONTENT requires accepted completed preflight with admitted scope'); END;
            CREATE TRIGGER content_completion BEFORE UPDATE ON sessions WHEN NEW.stage='CONTENT' AND NEW.state='COMPLETED' AND (
                (SELECT count(*) FROM content_hashes WHERE content_session_id=NEW.session_id)<>
                (SELECT count(*) FROM physical_files p WHERE p.preflight_id=NEW.parent_preflight_id AND EXISTS(
                    SELECT 1 FROM observation_memberships m JOIN component_memberships cm ON cm.preflight_id=m.preflight_id AND cm.observation_root_id=m.observation_root_id
                    JOIN candidate_components c ON c.preflight_id=cm.preflight_id AND c.component_id=cm.component_id
                    WHERE m.preflight_id=p.preflight_id AND m.entry_id=p.entry_id AND c.outcome='ADMITTED'))
                OR EXISTS(SELECT 1 FROM content_hashes WHERE content_session_id=NEW.session_id AND status='PENDING')
                OR (SELECT count(*) FROM content_component_outcomes WHERE content_session_id=NEW.session_id)<>
                   (SELECT count(*) FROM candidate_components WHERE preflight_id=NEW.parent_preflight_id AND outcome='ADMITTED')
                OR EXISTS(SELECT 1 FROM content_component_outcomes o JOIN candidate_components c ON c.component_id=o.component_id AND c.preflight_id=o.preflight_id
                    WHERE o.content_session_id=NEW.session_id AND ((SELECT count(*) FROM comparisons q WHERE q.content_session_id=o.content_session_id AND q.component_id=o.component_id)<>
                    CASE WHEN o.outcome='COMPLETE' THEN c.member_count*(c.member_count-1)/2 ELSE 0 END)))
            BEGIN SELECT RAISE(ABORT,'Incomplete CONTENT publication coverage'); END;
            """);
    }

    private const string ContentSchema = """
        CREATE TABLE content_hashes(content_session_id INTEGER NOT NULL,preflight_id INTEGER NOT NULL,physical_file_id INTEGER NOT NULL,
            status TEXT NOT NULL CHECK(status IN ('PENDING','SUCCESS','ERROR','UNSTABLE')),reason_code TEXT NOT NULL,
            sha256 TEXT,verified_size INTEGER CHECK(verified_size>=0),before_size INTEGER,before_mtime_ticks INTEGER,before_native_identity TEXT,
            after_size INTEGER,after_mtime_ticks INTEGER,after_native_identity TEXT,bytes_read INTEGER NOT NULL CHECK(bytes_read>=0),
            attempts INTEGER NOT NULL CHECK(attempts BETWEEN 0 AND 2),read_started_utc TEXT,read_ended_utc TEXT,
            PRIMARY KEY(content_session_id,physical_file_id),
            FOREIGN KEY(content_session_id,preflight_id) REFERENCES sessions(session_id,parent_preflight_id),
            FOREIGN KEY(preflight_id,physical_file_id) REFERENCES physical_files(preflight_id,physical_file_id),
            CHECK((status='SUCCESS' AND reason_code='CONTENT_READ_SUCCESS' AND sha256 IS NOT NULL AND length(sha256)=64 AND sha256 NOT GLOB '*[^0-9a-f]*'
                AND verified_size IS NOT NULL AND before_size IS NOT NULL AND after_size IS NOT NULL AND before_mtime_ticks IS NOT NULL AND after_mtime_ticks IS NOT NULL
                AND before_native_identity IS NOT NULL AND after_native_identity IS NOT NULL
                AND before_size=verified_size AND after_size=verified_size AND before_mtime_ticks=after_mtime_ticks
                AND before_native_identity=after_native_identity AND attempts>=1 AND bytes_read>=verified_size)
                OR (status<>'SUCCESS' AND sha256 IS NULL AND verified_size IS NULL)),
            CHECK((status='ERROR' AND reason_code='FILE_READ_ERROR') OR (status='UNSTABLE' AND reason_code='FILE_UNSTABLE' AND attempts=2)
                OR status IN ('PENDING','SUCCESS')),
            CHECK(status='PENDING' OR (attempts>=1 AND before_size IS NOT NULL AND after_size IS NOT NULL
                AND before_mtime_ticks IS NOT NULL AND after_mtime_ticks IS NOT NULL AND before_native_identity IS NOT NULL AND after_native_identity IS NOT NULL
                AND read_started_utc IS NOT NULL AND read_ended_utc IS NOT NULL)));
        CREATE INDEX content_digest ON content_hashes(content_session_id,sha256);
        CREATE TABLE content_component_outcomes(content_session_id INTEGER NOT NULL,preflight_id INTEGER NOT NULL,component_id INTEGER NOT NULL,
            outcome TEXT NOT NULL CHECK(outcome IN ('COMPLETE','INCOMPLETE')),reason_code TEXT NOT NULL,reason TEXT NOT NULL,
            required_physical_file_count INTEGER NOT NULL CHECK(required_physical_file_count>0),trusted_successful_hash_count INTEGER NOT NULL CHECK(trusted_successful_hash_count>=0),
            failed_untrusted_file_count INTEGER NOT NULL CHECK(failed_untrusted_file_count>=0),affected_observation_root_count INTEGER NOT NULL CHECK(affected_observation_root_count>=0),
            comparison_eligible INTEGER NOT NULL CHECK(comparison_eligible IN (0,1)),coherent INTEGER NOT NULL CHECK(coherent=1),sealed INTEGER NOT NULL CHECK(sealed=1),
            PRIMARY KEY(content_session_id,component_id),FOREIGN KEY(content_session_id,preflight_id) REFERENCES sessions(session_id,parent_preflight_id),
            FOREIGN KEY(preflight_id,component_id) REFERENCES candidate_components(preflight_id,component_id),
            CHECK(required_physical_file_count=trusted_successful_hash_count+failed_untrusted_file_count),
            CHECK((outcome='COMPLETE' AND reason_code='CONTENT_COMPLETE' AND failed_untrusted_file_count=0 AND affected_observation_root_count=0 AND comparison_eligible=1)
                OR (outcome='INCOMPLETE' AND reason_code='REQUIRED_CONTENT_UNTRUSTED' AND failed_untrusted_file_count>0 AND affected_observation_root_count>0 AND comparison_eligible=0)));
        CREATE TABLE comparisons(content_session_id INTEGER NOT NULL,preflight_id INTEGER NOT NULL,component_id INTEGER NOT NULL,root_a_id INTEGER NOT NULL,root_b_id INTEGER NOT NULL,
            same_path_same_bytes INTEGER NOT NULL CHECK(same_path_same_bytes>=0),same_path_different_bytes INTEGER NOT NULL CHECK(same_path_different_bytes>=0),
            files_only_a INTEGER NOT NULL CHECK(files_only_a>=0),files_only_b INTEGER NOT NULL CHECK(files_only_b>=0),
            directories_only_a INTEGER NOT NULL CHECK(directories_only_a>=0),directories_only_b INTEGER NOT NULL CHECK(directories_only_b>=0),kind_conflicts INTEGER NOT NULL CHECK(kind_conflicts>=0),
            tree_exact INTEGER NOT NULL CHECK(tree_exact IN (0,1)),content_multiset_exact INTEGER NOT NULL CHECK(content_multiset_exact IN (0,1)),
            non_chart_tree_exact INTEGER NOT NULL CHECK(non_chart_tree_exact IN (0,1)),non_chart_multiset_exact INTEGER NOT NULL CHECK(non_chart_multiset_exact IN (0,1)),
            path_conflict INTEGER NOT NULL CHECK(path_conflict IN (0,1)),fileset_variant INTEGER NOT NULL CHECK(fileset_variant IN (0,1)),
            PRIMARY KEY(content_session_id,root_a_id,root_b_id),UNIQUE(content_session_id,component_id,root_a_id,root_b_id),CHECK(root_a_id<>root_b_id),
            FOREIGN KEY(content_session_id,preflight_id) REFERENCES sessions(session_id,parent_preflight_id),
            FOREIGN KEY(content_session_id,component_id) REFERENCES content_component_outcomes(content_session_id,component_id),
            FOREIGN KEY(preflight_id,component_id,root_a_id) REFERENCES component_memberships(preflight_id,component_id,observation_root_id),
            FOREIGN KEY(preflight_id,component_id,root_b_id) REFERENCES component_memberships(preflight_id,component_id,observation_root_id));
        CREATE TABLE comparison_entries(content_session_id INTEGER NOT NULL,preflight_id INTEGER NOT NULL,component_id INTEGER NOT NULL,root_a_id INTEGER NOT NULL,root_b_id INTEGER NOT NULL,
            relative_path_key TEXT NOT NULL,category TEXT NOT NULL CHECK(category IN ('SAME_FILE','SAME_DIRECTORY','CONTENT_CONFLICT','FILE_ONLY_A','FILE_ONLY_B','DIRECTORY_ONLY_A','DIRECTORY_ONLY_B','KIND_CONFLICT')),
            entry_a_id INTEGER,entry_b_id INTEGER,sha256_a TEXT,sha256_b TEXT,
            PRIMARY KEY(content_session_id,root_a_id,root_b_id,relative_path_key),
            FOREIGN KEY(content_session_id,preflight_id) REFERENCES sessions(session_id,parent_preflight_id),
            FOREIGN KEY(content_session_id,component_id,root_a_id,root_b_id) REFERENCES comparisons(content_session_id,component_id,root_a_id,root_b_id),
            FOREIGN KEY(preflight_id,root_a_id,entry_a_id) REFERENCES observation_memberships(preflight_id,observation_root_id,entry_id),
            FOREIGN KEY(preflight_id,root_b_id,entry_b_id) REFERENCES observation_memberships(preflight_id,observation_root_id,entry_id));
        CREATE TABLE comparison_content_groups(content_session_id INTEGER NOT NULL,preflight_id INTEGER NOT NULL,component_id INTEGER NOT NULL,root_a_id INTEGER NOT NULL,root_b_id INTEGER NOT NULL,
            projection TEXT NOT NULL CHECK(projection IN ('FULL','NON_CHART')),sha256 TEXT NOT NULL CHECK(length(sha256)=64),
            count_a INTEGER NOT NULL CHECK(count_a>=0),count_b INTEGER NOT NULL CHECK(count_b>=0),differing_path_sets INTEGER NOT NULL CHECK(differing_path_sets IN (0,1)),
            PRIMARY KEY(content_session_id,root_a_id,root_b_id,projection,sha256),CHECK(count_a+count_b>0),
            FOREIGN KEY(content_session_id,preflight_id) REFERENCES sessions(session_id,parent_preflight_id),
            FOREIGN KEY(content_session_id,component_id,root_a_id,root_b_id) REFERENCES comparisons(content_session_id,component_id,root_a_id,root_b_id));
        """;
}
