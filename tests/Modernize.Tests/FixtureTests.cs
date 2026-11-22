using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Modernize.Tests;

internal static class Paths
{
    public static string Repo([CallerFilePath] string here = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(here)!);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "examples", "LegacyShop"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repository root not found");
    }

    public static string LegacyShop => Path.Combine(Repo(), "examples", "LegacyShop");
}

public class FixtureTests
{
    private static readonly DateOnly AsOf = new(2026, 11, 22);
    private static readonly ScanResult Result = Scanner.Scan(Paths.LegacyShop, new ScanOptions { AsOf = AsOf });

    private static List<string> Ids(string file) =>
        Result.Findings.Where(f => f.File == file).Select(f => f.Rule.Id).Distinct().OrderBy(x => x, StringComparer.Ordinal).ToList();

    private static ProjectAssessment Project(string name) => Result.Projects.Single(p => p.Project.Name == name);

    [Fact]
    public void Finds_the_blockers_in_each_file()
    {
        Assert.Equal(["LDM101"], Ids("LegacyShop.Web/Controllers/OrdersController.cs"));
        Assert.Equal(["LDM101", "LDM103"], Ids("LegacyShop.Web/Global.asax.cs"));
        Assert.Equal(["LDM102"], Ids("LegacyShop.Web/Checkout.aspx"));
        Assert.Equal(["LDM104"], Ids("LegacyShop.Web/Startup.cs"));
        Assert.Equal(["LDM110", "LDM112"], Ids("LegacyShop.Services/IOrderService.cs"));
        Assert.Equal(["LDM110"], Ids("LegacyShop.Services/OrderService.svc"));
        Assert.Equal(["LDM120", "LDM130", "LDM131", "LDM140"], Ids("LegacyShop.Core/Catalog.cs"));
        Assert.Equal(["LDM114", "LDM126"], Ids("LegacyShop.Core/Pricing.cs"));
        Assert.Equal(["LDM121", "LDM122", "LDM140"], Ids("LegacyShop.Worker/Program.cs"));
        Assert.Equal(["LDM124"], Ids("LegacyShop.Reports/Chart.cs"));
    }

    [Fact]
    public void Comments_and_string_literals_are_not_reported()
    {
        // Catalog.cs mentions HttpContext.Current, BinaryFormatter and new WebClient() in a comment and a string.
        var catalog = Result.Findings.Where(f => f.File == "LegacyShop.Core/Catalog.cs").ToList();
        Assert.DoesNotContain("LDM101", catalog.Select(f => f.Rule.Id));
        Assert.DoesNotContain("LDM114", catalog.Select(f => f.Rule.Id));
        Assert.Equal(29, Assert.Single(catalog, f => f.Rule.Id == "LDM120").Line);
    }

    [Fact]
    public void Configuration_findings_redact_secrets_and_ignore_placeholders()
    {
        var web = Result.Findings.Where(f => f.File == "LegacyShop.Web/web.config").ToList();
        Assert.Equal(["LDM141", "LDM141", "LDM142", "LDM150", "LDM151", "LDM152"], web.Select(f => f.Rule.Id).OrderBy(x => x, StringComparer.Ordinal).ToList());
        var secrets = web.Where(f => f.Rule.Id == "LDM141").Select(f => f.Evidence).ToList();
        Assert.Contains(secrets, e => e.Contains("\"Shop\"", StringComparison.Ordinal));
        Assert.Contains(secrets, e => e.Contains("PaymentApiKey", StringComparison.Ordinal));
        Assert.All(web, f => Assert.DoesNotContain("Summer2019", f.Evidence));
        Assert.All(web, f => Assert.DoesNotContain("FAKE", f.Evidence)); // machineKey and API key values
        Assert.Equal(["LDM153"], Ids("LegacyShop.Services/web.config"));
    }

    [Fact]
    public void Frameworks_are_judged_against_the_support_policy_on_the_as_of_date()
    {
        var unsupported = Result.Findings.Where(f => f.Rule.Id == "LDM003").Select(f => f.Project).OrderBy(x => x, StringComparer.Ordinal).ToList();
        Assert.Equal(["LegacyShop.Core", "LegacyShop.Reports", "LegacyShop.Worker"], unsupported); // net461 and net8.0
        var before = Scanner.Scan(Paths.LegacyShop, new ScanOptions { AsOf = new DateOnly(2026, 6, 1) });
        Assert.Equal(["LegacyShop.Reports", "LegacyShop.Worker"],
            before.Findings.Where(f => f.Rule.Id == "LDM004").Select(f => f.Project).OrderBy(x => x, StringComparer.Ordinal).ToList());
    }

    [Fact]
    public void Projects_are_classified_and_ordered_dependencies_first()
    {
        Assert.Equal(ProjectKind.Library, Project("LegacyShop.Core").Project.Kind);
        Assert.Equal(ProjectKind.Web, Project("LegacyShop.Web").Project.Kind);
        Assert.Equal(ProjectKind.WcfService, Project("LegacyShop.Services").Project.Kind);
        Assert.Equal(ProjectKind.Console, Project("LegacyShop.Worker").Project.Kind);
        Assert.Equal(ProjectKind.Test, Project("LegacyShop.Core.Tests").Project.Kind);
        Assert.False(Project("LegacyShop.Core").Project.SdkStyle);
        Assert.Equal(["net461"], Project("LegacyShop.Core").Project.TargetFrameworks);

        Assert.Equal(1, Project("LegacyShop.Core").Wave);
        Assert.Equal(1, Project("LegacyShop.Reports").Wave);
        Assert.All(["LegacyShop.Web", "LegacyShop.Services", "LegacyShop.Worker", "LegacyShop.Core.Tests"], n => Assert.Equal(2, Project(n).Wave));
    }

    [Fact]
    public void Recommendations_follow_project_kind_and_consumers()
    {
        Assert.StartsWith("net48;net10.0", Project("LegacyShop.Core").RecommendedTarget); // still used by .NET Framework apps
        Assert.StartsWith("net10.0 (new ASP.NET Core app", Project("LegacyShop.Web").RecommendedTarget);
        Assert.Equal("net10.0 with CoreWCF or gRPC", Project("LegacyShop.Services").RecommendedTarget);
        Assert.Equal("net10.0", Project("LegacyShop.Worker").RecommendedTarget);
        Assert.Contains(Project("LegacyShop.Web").Actions, a => a.Contains("YARP", StringComparison.Ordinal));
        Assert.Contains(Project("LegacyShop.Web").Actions, a => a.Contains("SystemWebAdapters", StringComparison.Ordinal));
        Assert.Contains(Project("LegacyShop.Worker").Actions, a => a.Contains("LDM121", StringComparison.Ordinal));
    }

    [Fact]
    public void Readiness_and_effort_reflect_severity()
    {
        Assert.Equal(0, Project("LegacyShop.Web").Readiness);
        Assert.Equal(86, Project("LegacyShop.Reports").Readiness); // one high (net8.0 out of support) + one medium
        Assert.Equal(45, Project("LegacyShop.Worker").Readiness);
        Assert.True(Project("LegacyShop.Web").EffortPoints > Project("LegacyShop.Worker").EffortPoints);
        Assert.Equal("XL", Project("LegacyShop.Web").Size);
    }

    [Fact]
    public void Sarif_and_json_reports_are_consistent()
    {
        using var sarif = JsonDocument.Parse(Reports.Sarif(Result));
        var run = sarif.RootElement.GetProperty("runs")[0];
        Assert.Equal("2.1.0", sarif.RootElement.GetProperty("version").GetString());
        var rules = run.GetProperty("tool").GetProperty("driver").GetProperty("rules").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ToList();
        var results = run.GetProperty("results").EnumerateArray().ToList();
        Assert.Equal(Result.Findings.Count, results.Count);
        Assert.All(results, r => Assert.Equal(r.GetProperty("ruleId").GetString(), rules[r.GetProperty("ruleIndex").GetInt32()]));
        Assert.Contains(results, r => r.GetProperty("level").GetString() == "error");

        using var json = JsonDocument.Parse(Reports.JsonReport(Result));
        Assert.Equal(Result.Count(Severity.Blocker), json.RootElement.GetProperty("summary").GetProperty("blocker").GetInt32());
        Assert.Equal(6, json.RootElement.GetProperty("projects").GetArrayLength());

        var md = Reports.Markdown(Result);
        Assert.Contains("## Plan", md);
        Assert.Contains("### Wave 2", md);
    }
}

public class EdgeCaseTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("modernize-").FullName;

    public void Dispose()
    {
        Directory.Delete(_dir, recursive: true);
        GC.SuppressFinalize(this);
    }

    private void Write(string relative, string content)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
    }

    [Fact]
    public void A_modern_project_has_nothing_to_do()
    {
        Write("App/App.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        Write("App/Program.cs", "using System.Text.Json;\nConsole.WriteLine(JsonSerializer.Serialize(1));\n");
        var result = Scanner.Scan(_dir, new ScanOptions { AsOf = new DateOnly(2026, 11, 22) });
        Assert.Empty(result.Findings);
        var a = Assert.Single(result.Projects);
        Assert.Equal(100, a.Readiness);
        Assert.Equal("no change needed", a.RecommendedTarget);
        Assert.Empty(a.Actions);
    }

    [Fact]
    public void Xml_with_a_dtd_is_skipped_not_expanded()
    {
        Write("Evil/Evil.csproj", "<?xml version=\"1.0\"?><!DOCTYPE p [<!ENTITY x SYSTEM \"file:///etc/passwd\">]><Project><PropertyGroup><TargetFramework>&x;</TargetFramework></PropertyGroup></Project>");
        Write("Evil/web.config", "<?xml version=\"1.0\"?><!DOCTYPE c [<!ENTITY x \"y\">]><configuration><system.web><authentication mode=\"Forms\"/></system.web></configuration>");
        var result = Scanner.Scan(_dir);
        Assert.Empty(result.Projects);
        Assert.Empty(result.Findings);
    }

    [Fact]
    public void Build_output_and_dependency_folders_are_not_scanned()
    {
        Write("App/obj/Generated.cs", "var f = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();");
        Write("node_modules/x/legacy.cs", "var f = new BinaryFormatter();");
        Assert.Empty(Scanner.Scan(_dir).Findings);
    }

    [Fact]
    public void Project_reference_cycles_do_not_hang_the_planner()
    {
        Write("A/A.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"../B/B.csproj\" /></ItemGroup></Project>");
        Write("B/B.csproj", "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net8.0</TargetFramework></PropertyGroup><ItemGroup><ProjectReference Include=\"..\\A\\A.csproj\" /></ItemGroup></Project>");
        var result = Scanner.Scan(_dir, new ScanOptions { AsOf = new DateOnly(2026, 11, 22) });
        Assert.Equal(2, result.Projects.Count);
        Assert.All(result.Projects, p => Assert.Equal(1, p.Wave));
    }
}
