using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Win32.SafeHandles;

namespace BmsLibraryAudit.Core;

internal enum PhaseSevenFixture { Compatible, Conflict, CaseConflict, Hardlinks }

// A nonpublic capability: no external source/evidence/destination is accepted.
// Only this factory can create an instance, and it creates the fixture itself.
internal sealed class PhaseSevenSyntheticWorkspace : IDisposable
{
    private readonly List<PhaseSevenDirectoryGuard> guards = [];
    private readonly Dictionary<string, NativePathFacts> owned = new(PathPolicy.Comparer);
    private readonly Dictionary<string, NativePathFacts> ownedInputs = new(PathPolicy.Comparer);
    internal string Root { get; }
    internal NativePathFacts Facts { get; }
    internal string Library => Path.Combine(Root, "sources");
    internal string SourceA => Path.Combine(Library, "A");
    internal string SourceB => Path.Combine(Library, "B");
    internal string Evidence => Path.Combine(Root, "phase2.db");
    internal string SourceDatabase => Path.Combine(Root, "audit.db");
    internal string PlanPath => Path.Combine(Root, "plan", "virtual_union_plan.json");
    internal string Stage => Path.Combine(Root, "stage");
    internal string ReceiptDirectory => Path.Combine(Root, "receipt");
    internal PhaseSixOptions Options { get; private set; } = null!;
    internal NativePathFacts? StageIdentity { get; private set; }
    private bool attempted;
    private bool disposed;
    private PhaseSevenSyntheticWorkspace(string root)
    {
        Root = root; PhaseSevenNative.CreateDirectory(root); Facts = PhaseTwoSafety.Inspect(root);
        guards.Add(new(root));
    }
    internal static PhaseSevenSyntheticWorkspace Create(PhaseSevenFixture scenario = PhaseSevenFixture.Compatible)
    {
        PhaseTwoSafety.RequirePlatform();
        var parent = PathPolicy.Absolute(AppContext.BaseDirectory); PathPolicy.RejectReparseAncestors(parent);
        var workspace = new PhaseSevenSyntheticWorkspace(Path.Combine(parent, "phase7a-" + Guid.NewGuid().ToString("N")));
        try { workspace.Initialize(scenario); return workspace; } catch { workspace.Dispose(); throw; }
    }
    private void Initialize(PhaseSevenFixture scenario)
    {
        NewDirectory(Library); NewDirectory(SourceA); NewDirectory(SourceB);
        NewDirectory(Path.Combine(SourceA, "empty")); NewDirectory(Path.Combine(SourceA, "sub"));
        var chart = Encoding.UTF8.GetBytes("#WAV01 kick.wav\r\n#BMP01 image.bmp\r\n#00111:01\r\n");
        var audio = Enumerable.Range(0, 65536 + 17).Select(i => (byte)(i % 251)).ToArray();
        foreach (var source in new[] { SourceA, SourceB }) {
            NewFile(Path.Combine(source, "chart.bms"), chart);
            NewFile(Path.Combine(source, "chart.pms"), [0xef, 0xbb, 0xbf, .. chart]);
            NewFile(Path.Combine(source, "chart.bmson"), Encoding.UTF8.GetBytes("{\"version\":\"1.0.0\",\"sound_channels\":[]}"));
            NewFile(Path.Combine(source, "kick.wav"), scenario == PhaseSevenFixture.Conflict && source == SourceB ? Encoding.UTF8.GetBytes("different B audio") : audio);
            NewFile(Path.Combine(source, source == SourceB && scenario == PhaseSevenFixture.CaseConflict ? "IMAGE.BMP" : "image.bmp"), Encoding.UTF8.GetBytes("synthetic BGA bytes"));
        }
        NewFile(Path.Combine(SourceA, "readme.txt"), Encoding.UTF8.GetBytes("exclusive auxiliary A"));
        NewFile(Path.Combine(SourceB, "unknown"), Encoding.UTF8.GetBytes("exclusive opaque B"));
        NewFile(Path.Combine(SourceA, "sub", "音.dat"), Encoding.UTF8.GetBytes("nested Unicode auxiliary"));
        NewFile(Path.Combine(SourceA, "kick-copy.wav"), audio); NewFile(Path.Combine(SourceB, "kick-alt.wav"), audio);
        if (scenario == PhaseSevenFixture.Hardlinks) {
            var alias = Path.Combine(SourceA, "hardlink.wav");
            if (!PhaseSevenNative.CreateHardLink(alias, Path.Combine(SourceA, "kick.wav"), IntPtr.Zero)) throw new IOException("Synthetic hardlink creation failed.");
        }
        foreach (var path in Directory.GetFileSystemEntries(Library, "*", SearchOption.AllDirectories).Append(Library)) owned.Add(path, PhaseTwoSafety.Inspect(path));
        var scan = new ChartScanner().Scan(new(SourceDatabase, [Library]));
        var preflight = new PhaseTwoPreflightRunner().Run(new(SourceDatabase, scan.ScanId, Evidence, Path.Combine(Root, "preflight")));
        var content = new PhaseTwoContentRunner().Run(new(Evidence, preflight.SessionId, preflight.ManifestSha256, Path.Combine(Root, "content")));
        using (var db = PhaseTwoDatabase.Open(Evidence, false)) {
            var component = (string)db.Scalar("SELECT component_key FROM candidate_components WHERE outcome='ADMITTED'")!;
            Options = new(Evidence, content.SessionId, content.ManifestSha256, component, [PathPolicy.Normalize(SourceA), PathPolicy.Normalize(SourceB)]);
        }
        PhaseSixVirtualUnionPlanner.Plan(Options, Path.GetDirectoryName(PlanPath)!);
        _ = PhaseSixPlanVerifier.Verify(Options, PlanPath); RequireBinding();
        foreach (var path in new[] { SourceDatabase, Evidence, PlanPath }) ownedInputs.Add(path, PhaseTwoSafety.Inspect(path));
    }
    private void NewDirectory(string path) { RequireBinding(); PhaseSevenNative.CreateDirectory(path); }
    private void NewFile(string path, byte[] bytes) { RequireBinding(); using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None); stream.Write(bytes); stream.Flush(true); }
    internal void RequireBinding()
    {
        if (disposed) throw new ObjectDisposedException(nameof(PhaseSevenSyntheticWorkspace));
        var facts = PhaseTwoSafety.Inspect(Root);
        if (facts.Identity != Facts.Identity || facts.ResolvedPath != Facts.ResolvedPath || facts.Attributes != Facts.Attributes) throw new IOException("Synthetic workspace binding changed.");
    }
    internal PhaseSixEnvelope AdmitPlan()
    {
        RequireBinding();
        foreach (var source in new[] { SourceA, SourceB }) {
            try {
                var facts = PhaseTwoSafety.Inspect(source);
                if (facts.Identity != owned[source].Identity || facts.ResolvedPath != owned[source].ResolvedPath || (facts.Attributes & (uint)FileAttributes.Directory) == 0)
                    throw new PhaseSevenChangedException(source);
            } catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or ReparsePointException) { throw new PhaseSevenChangedException(e.Message); }
        }
        var envelope = PhaseSixPlanVerifier.Verify(Options, PlanPath); var plan = envelope.Plan;
        if (plan.States.PhysicalUnionState != "UNION_PHYSICALLY_COMPATIBLE" || plan.States.ConservationState != "COMPLETE_ACCOUNTING" ||
            plan.Conflicts.Length != 0 || plan.Counts.BlockedAppearances != 0) throw new InvalidOperationException("Conflicting/incomplete plan cannot be staged.");
        var keys = new[] { PathPolicy.Normalize(SourceA), PathPolicy.Normalize(SourceB) }.Order(StringComparer.Ordinal);
        if (!plan.Roots.Select(r => r.Key).SequenceEqual(keys, StringComparer.Ordinal)) throw new InvalidOperationException("Plan is not for this owned workspace.");
        foreach (var entry in plan.Roots.SelectMany(r => r.Entries)) {
            if (!owned.TryGetValue(entry.OriginalPath, out var facts) || facts.Identity != entry.NativeIdentity || !PathPolicy.Contains(Library, entry.OriginalPath))
                throw new InvalidOperationException("Plan entry is not a factory-owned source appearance.");
        }
        foreach (var target in plan.VirtualEntries) _ = TargetPath(target.RelativePath);
        return envelope;
    }
    internal void ValidateDestination(string candidate)
    {
        RequireBinding(); candidate = PathPolicy.Absolute(candidate);
        if (!string.Equals(candidate, Stage, StringComparison.Ordinal) || PathPolicy.Contains(Library, candidate) || PhaseTwoSafety.Exists(candidate))
            throw new InvalidOperationException("Only the new fixed destination of this owned synthetic workspace is allowed.");
        PathPolicy.RejectReparseAncestors(candidate);
        var native = PhaseTwoSafety.Resolve(candidate);
        if (native != Facts.ResolvedPath + "\\STAGE") throw new IOException("Stage alias/containment mismatch.");
    }
    internal void RequireOwnedInput(string path, NativePathFacts facts)
    {
        RequireBinding();
        if (!ownedInputs.TryGetValue(path, out var expected) || expected != facts || facts.LinkCount != 1)
            throw new IOException("Input evidence/plan is not bound to its factory-owned native object.");
    }
    internal void BeginStage()
    {
        if (attempted) throw new InvalidOperationException("Synthetic staging never resumes or reuses a prior attempt.");
        attempted = true; ValidateDestination(Stage); PhaseSevenNative.CreateDirectory(Stage);
        var guard = new PhaseSevenDirectoryGuard(Stage); guards.Add(guard); StageIdentity = guard.Facts;
    }
    internal void GuardStageDirectory(string path) { guards.Add(new(path)); RequireStage(); }
    internal void RequireStage()
    {
        RequireBinding(); var actual = PhaseTwoSafety.Inspect(Stage);
        if (StageIdentity is null || actual.Identity != StageIdentity.Identity || actual.ResolvedPath != Facts.ResolvedPath + "\\STAGE" || actual.Attributes != StageIdentity.Attributes)
            throw new IOException("Owned stage binding changed.");
    }
    internal string TargetPath(string relative)
    {
        if (relative == ".") return Stage;
        if (string.IsNullOrEmpty(relative) || relative.Length > 160 || relative.Contains('\\') || Path.IsPathRooted(relative)) throw new InvalidOperationException("Invalid stage relative path.");
        var path = Stage;
        foreach (var segment in relative.Split('/')) {
            if (segment.Length == 0 || segment is "." or ".." || segment.EndsWith('.') || segment.EndsWith(' ') ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || Regex.IsMatch(segment, "^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\\.|$)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant) ||
                Regex.IsMatch(segment, "~[0-9]", RegexOptions.CultureInvariant)) throw new InvalidOperationException("Ambiguous/unsafe Win32 stage path segment.");
            path = Path.Combine(path, segment);
        }
        path = PathPolicy.Absolute(path);
        if (!PathPolicy.Contains(Stage, path) || path == Stage || path.Length >= 248) throw new InvalidOperationException("Stage path exceeds bounded Win32 scope.");
        return path;
    }
    public void Dispose() { if (disposed) return; foreach (var guard in guards.AsEnumerable().Reverse()) guard.Dispose(); disposed = true; }
}

internal static class PhaseSevenNative
{
    internal static void CreateDirectory(string path)
    {
        PathPolicy.RejectReparseAncestors(path);
        if (!CreateDirectoryNative(path, IntPtr.Zero)) throw new IOException("Exclusive synthetic directory creation refused: " + path, new Win32Exception(Marshal.GetLastWin32Error()));
    }
    [DllImport("kernel32.dll", EntryPoint = "CreateDirectoryW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CreateDirectoryNative(string path, IntPtr security);
    [DllImport("kernel32.dll", EntryPoint = "CreateHardLinkW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] internal static extern bool CreateHardLink(string path, string existing, IntPtr security);
    [DllImport("kernel32.dll", EntryPoint = "CreateFileW", CharSet = CharSet.Unicode, SetLastError = true)]
    internal static extern SafeFileHandle OpenDirectory(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
}
internal sealed class PhaseSevenDirectoryGuard : IDisposable
{
    private readonly SafeFileHandle handle;
    internal NativePathFacts Facts { get; }
    internal PhaseSevenDirectoryGuard(string path)
    {
        var before = PhaseTwoSafety.Inspect(path);
        handle = PhaseSevenNative.OpenDirectory(path, 0, 1, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid) { handle.Dispose(); throw new IOException("Cannot pin synthetic directory identity."); }
        try {
            Facts = PhaseTwoSafety.Inspect(handle, path);
            if (Facts != before || (Facts.Attributes & (uint)FileAttributes.Directory) == 0) throw new IOException("Directory binding changed before guard.");
        } catch { handle.Dispose(); throw; }
    }
    public void Dispose() => handle.Dispose();
}
