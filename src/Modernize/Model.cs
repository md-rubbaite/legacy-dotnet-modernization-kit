namespace Modernize;

public enum Severity
{
    Info = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Blocker = 4,
}

/// <param name="Effort">Relative effort points for fixing this rule once in one project.</param>
public sealed record Rule(string Id, string Title, Severity Severity, string Category, int Effort, string Guidance);

/// <param name="File">Path relative to the scanned root, with forward slashes.</param>
/// <param name="Evidence">The matching text, trimmed; secrets are redacted.</param>
public sealed record Finding(Rule Rule, string File, int Line, string Evidence, string? Project);

public enum ProjectKind
{
    Library,
    Web,
    WcfService,
    Console,
    Desktop,
    Test,
}

public sealed record PackageRef(string Id, string? Version);

public sealed record ProjectInfo(
    string File,
    string Name,
    bool SdkStyle,
    IReadOnlyList<string> TargetFrameworks,
    ProjectKind Kind,
    IReadOnlyList<string> ProjectReferences,
    IReadOnlyList<PackageRef> Packages,
    bool UsesPackagesConfig);

public sealed record ProjectAssessment(
    ProjectInfo Project,
    int Readiness,
    int EffortPoints,
    string Size,
    int Wave,
    string RecommendedTarget,
    IReadOnlyList<string> Actions,
    IReadOnlyDictionary<Severity, int> CountsBySeverity);

public sealed record ScanResult(
    string Root,
    DateOnly AsOf,
    IReadOnlyList<ProjectAssessment> Projects,
    IReadOnlyList<Finding> Findings,
    int FilesScanned)
{
    public int Count(Severity atLeast) => Findings.Count(f => f.Rule.Severity >= atLeast);
}
