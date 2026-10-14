using System.Globalization;
using System.Text.Json;

namespace BmsLibraryAudit.Core;

internal static class PhaseThreeBmsonParser
{
    internal static void Parse(ChartSyntaxBuilder b, ChartDecoding decoding)
    {
        try
        {
            using var document = JsonDocument.Parse(decoding.Text, new JsonDocumentOptions { MaxDepth = b.Limits.MaximumJsonDepth });
            var root = document.RootElement; if (!Object(root, "")) return;
            Check(root, "", "version info lines bpm_events stop_events sound_channels bga");
            var versions = Values(root, "version", "").ToArray();
            foreach (var v in versions) if (v.ValueKind == JsonValueKind.String) b.Versions.Add(v.GetString()!); else Error("VERSION_TYPE", "/version", v);
            var supported = versions.Length == 1 && versions[0].ValueKind == JsonValueKind.String && versions[0].GetString() == "1.0.0";
            if (!supported) b.Diagnostic("BMSON_VERSION_UNSUPPORTED", "UNSUPPORTED", "Only explicit BMSON 1.0.0 dependency syntax is supported. Known fields remain observable; legacy/unknown semantics are not inferred.");
            foreach (var info in Values(root, "info", ""))
            {
                if (!Object(info, "/info")) continue;
                Check(info, "/info", "title subtitle artist subartists genre mode_hint chart_name level init_bpm judge_rank total back_image eyecatch_image banner_image preview_music resolution");
                foreach (var key in new[] { "back_image", "eyecatch_image", "banner_image", "preview_music" })
                    foreach (var value in Values(info, key, "/info"))
                    {
                        if (value.ValueKind == JsonValueKind.Null) continue;
                        if (value.ValueKind != JsonValueKind.String) { Error("RESOURCE_PATH_TYPE", "/info/" + key, value); continue; }
                        var source = At("/info/" + key, value);
                        b.Declare("BMSON_INFO", key, key == "preview_music" ? "AUDIO" : "VISUAL", value.GetString(), value.GetRawText(), source, state: supported ? "OBSERVED" : "UNSUPPORTED");
                        b.Use("BMSON_INFO", key, "HEADER_RESOURCE", source);
                    }
                foreach (var p in info.EnumerateObject().Where(p => "title subtitle artist genre mode_hint chart_name".Split(' ').Contains(p.Name, StringComparer.Ordinal)))
                    if (p.Value.ValueKind != JsonValueKind.String) Error("INFO_FIELD_TYPE", "/info/" + p.Name, p.Value);
            }
            var channelNumber = 0;
            foreach (var channels in Values(root, "sound_channels", "", required: true))
                foreach (var (channel, path) in Items(channels, "/sound_channels"))
                {
                    if (!Object(channel, path)) continue; Check(channel, path, "name notes");
                    var logical = (channelNumber++).ToString(CultureInfo.InvariantCulture);
                    foreach (var name in Values(channel, "name", path, required: true))
                    {
                        if (name.ValueKind != JsonValueKind.String) { Error("RESOURCE_PATH_TYPE", path + "/name", name); continue; }
                        b.Declare("BMSON_SOUND", logical, "AUDIO", name.GetString(), name.GetRawText(), At(path + "/name", name), state: supported ? "OBSERVED" : "UNSUPPORTED");
                    }
                    foreach (var notes in Values(channel, "notes", path, required: true))
                        foreach (var (note, notePath) in Items(notes, path + "/notes"))
                        {
                            if (!Object(note, notePath)) continue; Check(note, notePath, "x y l c");
                            var xs = Values(note, "x", notePath, required: true).ToArray(); var lane = xs.Length == 1 ? xs[0].GetRawText() : null;
                            var kind = "UNKNOWN_SOUND_EVENT";
                            if (xs.Length == 1 && (xs[0].ValueKind == JsonValueKind.Null || xs[0].ValueKind == JsonValueKind.Number && xs[0].TryGetInt64(out var x) && x >= 0))
                                kind = xs[0].ValueKind == JsonValueKind.Null || xs[0].GetInt64() == 0 ? "BGM_EVENT" : "PLAYABLE_EVENT";
                            else Error("LANE_UNSUPPORTED", notePath, note);
                            var pulse = Unsigned(note, "y", notePath); var length = Unsigned(note, "l", notePath);
                            var cs = Values(note, "c", notePath, required: true).ToArray(); bool? continuation = null;
                            if (cs.Length == 1 && cs[0].ValueKind is JsonValueKind.True or JsonValueKind.False) continuation = cs[0].GetBoolean();
                            else Error("CONTINUATION_TYPE", notePath, note);
                            b.Use("BMSON_SOUND", logical, kind, At(notePath, note), state: supported && kind != "UNKNOWN_SOUND_EVENT" && pulse is not null && length is not null && continuation is not null ? "POTENTIAL" : "UNKNOWN", lane: lane, pulse: pulse, length: length, continuation: continuation);
                        }
                }
            foreach (var bga in Values(root, "bga", ""))
            {
                if (!Object(bga, "/bga")) continue; Check(bga, "/bga", "bga_header bga_events layer_events poor_events");
                foreach (var headers in Values(bga, "bga_header", "/bga"))
                    foreach (var (header, path) in Items(headers, "/bga/bga_header"))
                    {
                        if (!Object(header, path)) continue; Check(header, path, "id name");
                        var ids = Values(header, "id", path, required: true).Select(v => Number(v, path + "/id")).ToArray();
                        var headerId = ids.Length == 1 ? ids[0] : null;
                        if (ids.Length > 1) b.Diagnostic("AMBIGUOUS_BGA_HEADER_ID", "AMBIGUOUS", "Multiple ID properties are retained without inventing an ID/name cross-product.", At(path, header));
                        foreach (var name in Values(header, "name", path, required: true))
                        {
                            if (name.ValueKind != JsonValueKind.String) { Error("RESOURCE_PATH_TYPE", path + "/name", name); continue; }
                            b.Declare("BMSON_BGA", headerId?.ToString(CultureInfo.InvariantCulture), "VISUAL", name.GetString(), name.GetRawText(), At(path + "/name", name), state: headerId is null ? "UNKNOWN" : supported ? "OBSERVED" : "UNSUPPORTED");
                        }
                    }
                foreach (var key in new[] { "bga_events", "layer_events", "poor_events" })
                    foreach (var events in Values(bga, key, "/bga"))
                        foreach (var (entry, path) in Items(events, "/bga/" + key))
                        {
                            if (!Object(entry, path)) continue; Check(entry, path, "id y"); var pulse = Unsigned(entry, "y", path);
                            foreach (var value in Values(entry, "id", path, required: true))
                            {
                                var id = Number(value, path + "/id"); b.Use("BMSON_BGA", id?.ToString(CultureInfo.InvariantCulture), key.ToUpperInvariant(), At(path, entry), state: supported && id is not null && pulse is not null ? "POTENTIAL" : "UNKNOWN", pulse: pulse);
                            }
                        }
            }
            foreach (var (key, fields) in new[] { ("lines", "y"), ("bpm_events", "y bpm"), ("stop_events", "y duration") })
                foreach (var array in Values(root, key, ""))
                    foreach (var (entry, path) in Items(array, "/" + key)) if (Object(entry, path)) Check(entry, path, fields);

            ChartSourcePosition At(string path, JsonElement e) => new(decoding.Id, null, null, path, e.GetRawText());
            void Error(string code, string path, JsonElement e) => b.Diagnostic(code, code == "DUPLICATE_JSON_PROPERTY" ? "AMBIGUOUS" : "UNKNOWN", "Unexpected, missing or contradictory BMSON field; raw JSON remains retained.", At(path, e));
            bool Object(JsonElement e, string path) { if (e.ValueKind == JsonValueKind.Object) return true; Error("OBJECT_TYPE", path, e); return false; }
            void Check(JsonElement e, string path, string fields)
            {
                var allowed = fields.Split(' ').ToHashSet(StringComparer.Ordinal); var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var p in e.EnumerateObject())
                {
                    b.Fact(); var pointer = path + "/" + p.Name.Replace("~", "~0", StringComparison.Ordinal).Replace("/", "~1", StringComparison.Ordinal);
                    if (!seen.Add(p.Name)) Error("DUPLICATE_JSON_PROPERTY", pointer, p.Value);
                    if (!allowed.Contains(p.Name)) b.Unrecognized("JSON_PROPERTY", At(pointer, p.Value));
                }
            }
            IEnumerable<JsonElement> Values(JsonElement e, string name, string path, bool required = false)
            {
                var found = false;
                foreach (var p in e.EnumerateObject()) if (p.Name == name) { found = true; yield return p.Value; }
                if (!found && required) Error("MISSING_FIELD", path + "/" + name, e);
            }
            IEnumerable<(JsonElement Value, string Path)> Items(JsonElement e, string path)
            {
                if (e.ValueKind != JsonValueKind.Array) { Error("ARRAY_TYPE", path, e); yield break; }
                var i = 0; foreach (var entry in e.EnumerateArray()) { b.Fact(); yield return (entry, path + "/" + (i++).ToString(CultureInfo.InvariantCulture)); }
            }
            long? Number(JsonElement e, string path)
            {
                if (e.ValueKind == JsonValueKind.Number && e.TryGetUInt32(out var value)) return value;
                Error("UNSIGNED_INTEGER_TYPE", path, e); return null;
            }
            long? Unsigned(JsonElement e, string name, string path)
            {
                var values = Values(e, name, path, required: true).ToArray();
                if (values.Length == 1) return Number(values[0], path + "/" + name);
                return null;
            }
        }
        catch (JsonException exception)
        {
            b.Diagnostic("INVALID_OR_DEPTH_LIMIT_JSON", "UNKNOWN", exception.Message, new(decoding.Id, (int?)exception.LineNumber + 1, (int?)exception.BytePositionInLine + 1, null, ""));
        }
    }
}
