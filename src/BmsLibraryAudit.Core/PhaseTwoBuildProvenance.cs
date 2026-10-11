using System.Reflection;

namespace BmsLibraryAudit.Core;

public sealed record PhaseTwoBuildProvenance(string InformationalVersion, string? Revision, string State)
{
    public string EvidenceRevision => State == "CLEAN" && Revision is not null ? Revision : State == "DIRTY" ? "UNKNOWN_DIRTY" : "UNKNOWN";
    public static PhaseTwoBuildProvenance Current => Resolve(
        typeof(PhaseTwoBuildProvenance).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
        typeof(PhaseTwoBuildProvenance).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().SingleOrDefault(a => a.Key == "PhaseTwoBuildState")?.Value);

    internal static PhaseTwoBuildProvenance Resolve(string? informational, string? state)
    {
        var suffix = informational?.Split('+').ElementAtOrDefault(1);
        var revision = suffix is { Length: 40 } && suffix.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f') ? suffix : null;
        return new(informational ?? "UNKNOWN", revision, revision is not null && state is "CLEAN" or "DIRTY" ? state : "UNKNOWN");
    }
}
