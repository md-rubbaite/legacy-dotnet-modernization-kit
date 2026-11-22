namespace Modernize;

/// <summary>Scores each project and orders the migration so dependencies move before their consumers.</summary>
internal static class Planner
{
    private static readonly Dictionary<Severity, int> Penalty = new()
    {
        [Severity.Blocker] = 25,
        [Severity.High] = 10,
        [Severity.Medium] = 4,
        [Severity.Low] = 1,
        [Severity.Info] = 0,
    };

    public static IReadOnlyList<ProjectAssessment> Assess(IReadOnlyList<ProjectInfo> projects, IReadOnlyList<Finding> findings)
    {
        var byFile = projects.ToDictionary(p => p.File, StringComparer.Ordinal);
        var waves = Waves(projects, byFile);
        var consumers = projects
            .SelectMany(p => p.ProjectReferences.Where(byFile.ContainsKey).Select(r => (Dependency: r, Consumer: p)))
            .ToLookup(x => x.Dependency, x => x.Consumer, StringComparer.Ordinal);

        var result = new List<ProjectAssessment>();
        foreach (var p in projects)
        {
            var mine = findings.Where(f => f.Project == p.Name).ToList();
            var distinct = mine.GroupBy(f => f.Rule.Id, StringComparer.Ordinal).ToList();
            var readiness = Math.Max(0, 100 - distinct.Sum(g => Penalty[g.First().Rule.Severity]));
            var effort = distinct.Sum(g => g.First().Rule.Effort + (g.Count() - 1) / 10);
            var counts = Enum.GetValues<Severity>().ToDictionary(s => s, s => mine.Count(f => f.Rule.Severity == s));
            var rules = distinct.Select(g => g.First().Rule).ToHashSet();
            var frameworkConsumers = consumers[p.File].Any(c => c.TargetFrameworks.Any(SupportPolicy.IsNetFramework));
            var target = Target(p, rules, frameworkConsumers);
            result.Add(new ProjectAssessment(p, readiness, effort, Size(effort), waves[p.File], target, Actions(p, rules, frameworkConsumers, target), counts));
        }
        return result.OrderBy(a => a.Wave).ThenBy(a => a.Project.Name, StringComparer.Ordinal).ToList();
    }

    /// <summary>Wave 1 has no project dependencies; wave n depends only on earlier waves. Cycles go last.</summary>
    private static Dictionary<string, int> Waves(IReadOnlyList<ProjectInfo> projects, Dictionary<string, ProjectInfo> byFile)
    {
        var wave = new Dictionary<string, int>(StringComparer.Ordinal);
        var remaining = projects.ToList();
        for (var n = 1; remaining.Count > 0; n++)
        {
            var ready = remaining.Where(p => p.ProjectReferences.Where(byFile.ContainsKey).All(wave.ContainsKey)).ToList();
            if (ready.Count == 0)
            {
                foreach (var p in remaining) wave[p.File] = n; // dependency cycle: break it by hand
                break;
            }
            foreach (var p in ready) wave[p.File] = n;
            remaining.RemoveAll(ready.Contains);
        }
        return wave;
    }

    private static string Target(ProjectInfo p, HashSet<Rule> rules, bool frameworkConsumers)
    {
        var modern = p.TargetFrameworks.Count > 0
            && p.TargetFrameworks.All(t => !SupportPolicy.IsNetFramework(t) && (SupportPolicy.IsNetStandard(t) || SupportPolicy.EndOf(t) is not { } end || end >= SupportPolicy.CurrentLtsEnds));
        if (modern) return "no change needed";
        return p.Kind switch
        {
            ProjectKind.Desktop => "net10.0-windows",
            ProjectKind.Library when frameworkConsumers => "net48;net10.0 (multi-target while .NET Framework apps still consume it)",
            ProjectKind.Web when rules.Contains(Rules.SystemWeb) || rules.Contains(Rules.WebForms) => "net10.0 (new ASP.NET Core app, migrated route by route)",
            ProjectKind.WcfService => "net10.0 with CoreWCF or gRPC",
            _ => SupportPolicy.CurrentLts,
        };
    }

    private static List<string> Actions(ProjectInfo p, HashSet<Rule> rules, bool frameworkConsumers, string target)
    {
        var actions = new List<string>();
        if (target == "no change needed" && rules.Count == 0) return actions;
        if (!p.SdkStyle) actions.Add("Convert the project file to SDK-style.");
        if (p.UsesPackagesConfig) actions.Add("Move packages.config to PackageReference.");
        var blockers = rules.Where(r => r.Severity == Severity.Blocker).OrderBy(r => r.Id, StringComparer.Ordinal).ToList();
        if (p.Kind == ProjectKind.Web && (rules.Contains(Rules.SystemWeb) || rules.Contains(Rules.WebForms)))
        {
            actions.Add("Stand up an ASP.NET Core (net10.0) app in front of this one with YARP; forward every route to the old app.");
            actions.Add("Share authentication and session with Microsoft.AspNetCore.SystemWebAdapters, then move routes one at a time, highest value or lowest risk first.");
            actions.Add("Retire the old app when the proxy no longer forwards any route.");
            blockers = blockers.Where(r => r != Rules.SystemWeb && r != Rules.WebForms).ToList();
        }
        else if (p.Kind == ProjectKind.WcfService || rules.Contains(Rules.WcfService))
        {
            actions.Add("Port service contracts to CoreWCF so existing SOAP clients keep working; offer gRPC or REST to new clients.");
            blockers = blockers.Where(r => r != Rules.WcfService).ToList();
        }
        foreach (var b in blockers) actions.Add($"Remove blocker {b.Id} ({b.Title}).");
        if (rules.Contains(Rules.PlaintextSecret)) actions.Add("Move secrets out of config files and rotate them.");
        if (p.Kind == ProjectKind.Library && frameworkConsumers)
            actions.Add("Multi-target net48;net10.0 so .NET Framework consumers keep building while modern ones move.");
        else if (target is not "no change needed" && !target.StartsWith("net10.0 (", StringComparison.Ordinal) && p.Kind != ProjectKind.WcfService)
            actions.Add($"Retarget to {target}; run the tests; review breaking changes of every skipped version.");
        return actions;
    }

    private static string Size(int effort) => effort switch
    {
        0 => "-",
        <= 3 => "XS",
        <= 8 => "S",
        <= 21 => "M",
        <= 55 => "L",
        _ => "XL",
    };
}
