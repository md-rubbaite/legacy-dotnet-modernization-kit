using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace Modernize;

public sealed class ScanOptions
{
    /// <summary>Date used for end-of-support checks. Default: today (UTC).</summary>
    public DateOnly AsOf { get; init; } = DateOnly.FromDateTime(DateTime.UtcNow);

    /// <summary>Files larger than this are skipped. Default 2 MB.</summary>
    public long MaxFileBytes { get; init; } = 2 * 1024 * 1024;

    public IReadOnlyCollection<string> ExcludedDirectories { get; init; } =
        ["bin", "obj", ".git", ".vs", "node_modules", "packages", "TestResults", "artifacts"];
}

/// <summary>Walks a directory tree and produces findings, project metadata and a migration plan.</summary>
public static partial class Scanner
{
    private static readonly string[] ProjectExtensions = [".csproj", ".vbproj", ".fsproj"];
    private static readonly string[] WebFormsExtensions = [".aspx", ".ascx", ".master", ".asmx"];

    public static ScanResult Scan(string root, ScanOptions? options = null)
    {
        options ??= new ScanOptions();
        var fullRoot = Path.GetFullPath(root);
        if (!Directory.Exists(fullRoot)) throw new DirectoryNotFoundException(fullRoot);

        var files = Enumerate(fullRoot, options).ToList();
        var projects = files
            .Where(f => ProjectExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .Select(f => ReadProject(f))
            .Where(p => p is not null)
            .Select(p => p!)
            .OrderBy(p => p.File, StringComparer.Ordinal)
            .ToList();
        var projectDirs = projects
            .Select(p => (Dir: Path.GetDirectoryName(p.File)! + Path.DirectorySeparatorChar, Project: p))
            .OrderByDescending(x => x.Dir.Length)
            .ToList();
        string? Owner(string file) => projectDirs.FirstOrDefault(x => file.StartsWith(x.Dir, StringComparison.Ordinal)).Project?.Name;
        string Rel(string file) => Path.GetRelativePath(fullRoot, file).Replace('\\', '/');

        var findings = new List<Finding>();
        void Add(Rule rule, string file, int line, string evidence) =>
            findings.Add(new Finding(rule, Rel(file), line, Trim(evidence), Owner(file)));

        foreach (var p in projects)
        {
            if (!p.SdkStyle) Add(Rules.LegacyProjectFormat, p.File, 1, "<Project ToolsVersion=…> without Sdk");
            if (p.UsesPackagesConfig) Add(Rules.PackagesConfig, Path.Combine(Path.GetDirectoryName(p.File)!, "packages.config"), 1, "packages.config");
            foreach (var tfm in p.TargetFrameworks)
            {
                var status = SupportPolicy.Describe(tfm, options.AsOf);
                if (SupportPolicy.IsNetFramework(tfm)) Add(Rules.NetFrameworkTarget, p.File, 1, $"{SupportPolicy.Normalize(tfm)}: {status}");
                if (SupportPolicy.EndOf(tfm) is { } end)
                {
                    if (end < options.AsOf) Add(Rules.UnsupportedFramework, p.File, 1, $"{SupportPolicy.Normalize(tfm)}: {status}");
                    else if (end <= options.AsOf.AddDays(365)) Add(Rules.FrameworkEndingSoon, p.File, 1, $"{SupportPolicy.Normalize(tfm)}: {status}");
                }
            }
            foreach (var package in p.Packages)
            {
                foreach (var (id, replacement) in Rules.Packages)
                {
                    if (id.IsMatch(package.Id))
                    {
                        Add(Rules.LegacyPackage, p.File, 1, $"{package.Id} {package.Version} → {replacement}");
                        break;
                    }
                }
            }
        }

        var scanned = 0;
        foreach (var file in files)
        {
            var name = Path.GetFileName(file);
            var ext = Path.GetExtension(file);
            if (ext.Equals(".cs", StringComparison.OrdinalIgnoreCase))
            {
                scanned++;
                var source = File.ReadAllText(file);
                var code = CSharpSanitizer.Sanitize(source); // same length and line breaks as the source
                var lines = source.Split('\n');
                foreach (var (rule, pattern) in Rules.CodePatterns)
                {
                    foreach (Match m in pattern.Matches(code))
                    {
                        var line = LineOf(code, m.Index);
                        Add(rule, file, line, lines[line - 1]);
                    }
                }
            }
            else if (WebFormsExtensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
            {
                scanned++;
                Add(Rules.WebForms, file, 1, name);
            }
            else if (ext.Equals(".svc", StringComparison.OrdinalIgnoreCase))
            {
                scanned++;
                Add(Rules.WcfService, file, 1, name);
            }
            else if (name.Equals("Global.asax", StringComparison.OrdinalIgnoreCase))
            {
                scanned++;
                Add(Rules.GlobalAsax, file, 1, name);
            }
            else if (name.Equals("web.config", StringComparison.OrdinalIgnoreCase) || name.Equals("app.config", StringComparison.OrdinalIgnoreCase))
            {
                scanned++;
                ScanConfig(file, Add);
            }
        }

        var assessments = Planner.Assess(projects, findings);
        var ordered = findings
            .OrderByDescending(f => f.Rule.Severity)
            .ThenBy(f => f.Rule.Id, StringComparer.Ordinal)
            .ThenBy(f => f.File, StringComparer.Ordinal)
            .ThenBy(f => f.Line)
            .ToList();
        return new ScanResult(fullRoot, options.AsOf, assessments, ordered, scanned + projects.Count);
    }

    private static IEnumerable<string> Enumerate(string root, ScanOptions options)
    {
        var excluded = new HashSet<string>(options.ExcludedDirectories, StringComparer.OrdinalIgnoreCase);
        var pending = new Stack<string>();
        pending.Push(root);
        while (pending.Count > 0)
        {
            var dir = pending.Pop();
            IEnumerable<string> entries;
            try
            {
                entries = Directory.EnumerateFileSystemEntries(dir).OrderBy(e => e, StringComparer.Ordinal).ToList();
            }
            catch (UnauthorizedAccessException)
            {
                continue;
            }
            foreach (var entry in entries)
            {
                var info = new FileInfo(entry);
                if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue; // do not follow links out of the tree
                if (info.Attributes.HasFlag(FileAttributes.Directory))
                {
                    if (!excluded.Contains(info.Name)) pending.Push(entry);
                }
                else if (info.Length <= options.MaxFileBytes)
                {
                    yield return entry;
                }
            }
        }
    }

    internal static ProjectInfo? ReadProject(string file)
    {
        XDocument doc;
        try
        {
            doc = LoadXml(file);
        }
        catch (XmlException)
        {
            return null;
        }
        var root = doc.Root;
        if (root is null || root.Name.LocalName != "Project") return null;
        var all = root.Descendants().ToList();
        string? Prop(string name) => all.FirstOrDefault(e => e.Name.LocalName == name && !e.HasElements)?.Value.Trim();

        var sdk = root.Attribute("Sdk")?.Value
            ?? all.FirstOrDefault(e => e.Name.LocalName == "Sdk")?.Attribute("Name")?.Value
            ?? all.FirstOrDefault(e => e.Name.LocalName == "Import" && e.Attribute("Sdk") is not null)?.Attribute("Sdk")?.Value;
        var sdkStyle = sdk is not null;

        var tfms = new List<string>();
        if (Prop("TargetFrameworks") is { Length: > 0 } many) tfms.AddRange(many.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        else if (Prop("TargetFramework") is { Length: > 0 } one) tfms.Add(one);
        else if (Prop("TargetFrameworkVersion") is { Length: > 0 } legacy) tfms.Add(SupportPolicy.Normalize(legacy));

        var dir = Path.GetDirectoryName(file)!;
        var refs = all
            .Where(e => e.Name.LocalName == "ProjectReference" && e.Attribute("Include") is not null)
            .Select(e => Path.GetFullPath(Path.Combine(dir, e.Attribute("Include")!.Value.Replace('\\', Path.DirectorySeparatorChar))))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var packages = all
            .Where(e => e.Name.LocalName == "PackageReference" && e.Attribute("Include") is not null)
            .Select(e => new PackageRef(e.Attribute("Include")!.Value, e.Attribute("Version")?.Value ?? e.Elements().FirstOrDefault(c => c.Name.LocalName == "Version")?.Value))
            .ToList();
        var packagesConfig = Path.Combine(dir, "packages.config");
        var usesPackagesConfig = File.Exists(packagesConfig);
        if (usesPackagesConfig)
        {
            try
            {
                packages.AddRange(LoadXml(packagesConfig).Descendants("package")
                    .Where(e => e.Attribute("id") is not null)
                    .Select(e => new PackageRef(e.Attribute("id")!.Value, e.Attribute("version")?.Value)));
            }
            catch (XmlException)
            {
                // unreadable packages.config: still reported as packages.config
            }
        }
        var references = all.Where(e => e.Name.LocalName == "Reference").Select(e => e.Attribute("Include")?.Value ?? "").ToList();

        var name = Path.GetFileNameWithoutExtension(file);
        var guids = Prop("ProjectTypeGuids")?.ToUpperInvariant() ?? "";
        var kind = ProjectKind.Library;
        bool HasFiles(string pattern) => Directory.EnumerateFiles(dir, pattern, SearchOption.TopDirectoryOnly).Any();
        if (packages.Any(p => p.Id is "Microsoft.NET.Test.Sdk" or "xunit" or "NUnit" or "MSTest.TestFramework") || name.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase))
            kind = ProjectKind.Test;
        else if (guids.Contains("3D9AD99F-2412-4246-B90B-4EAA41C64699", StringComparison.Ordinal) || HasFiles("*.svc"))
            kind = ProjectKind.WcfService;
        else if (sdk?.EndsWith(".Web", StringComparison.OrdinalIgnoreCase) == true || guids.Contains("349C5851-65DF-11DA-9384-00065B846F21", StringComparison.Ordinal) || HasFiles("Global.asax") || HasFiles("web.config"))
            kind = ProjectKind.Web;
        else if (string.Equals(Prop("UseWindowsForms"), "true", StringComparison.OrdinalIgnoreCase) || string.Equals(Prop("UseWPF"), "true", StringComparison.OrdinalIgnoreCase)
                 || references.Any(r => r.StartsWith("System.Windows.Forms", StringComparison.Ordinal) || r.StartsWith("PresentationFramework", StringComparison.Ordinal))
                 || string.Equals(Prop("OutputType"), "WinExe", StringComparison.OrdinalIgnoreCase))
            kind = ProjectKind.Desktop;
        else if (string.Equals(Prop("OutputType"), "Exe", StringComparison.OrdinalIgnoreCase))
            kind = ProjectKind.Console;

        return new ProjectInfo(file, name, sdkStyle, tfms, kind, refs, packages, usesPackagesConfig);
    }

    private static void ScanConfig(string file, Action<Rule, string, int, string> add)
    {
        XDocument doc;
        try
        {
            doc = LoadXml(file);
        }
        catch (XmlException)
        {
            return;
        }
        static int Line(XObject o) => o is IXmlLineInfo info && info.HasLineInfo() ? info.LineNumber : 1;
        foreach (var e in doc.Descendants())
        {
            var n = e.Name.LocalName;
            var parent = e.Parent?.Name.LocalName;
            if (n == "authentication" && parent == "system.web" && e.Attribute("mode")?.Value is "Forms" or "Windows")
                add(Rules.FormsAuthentication, file, Line(e), $"<authentication mode=\"{e.Attribute("mode")!.Value}\">");
            else if ((n is "httpModules" or "httpHandlers" && parent == "system.web") || (n is "modules" or "handlers" && parent == "system.webServer" && e.Elements().Any(c => c.Name.LocalName == "add" && c.Attribute("type") is not null)))
                add(Rules.ModulesHandlers, file, Line(e), $"<{n}>");
            else if (n == "sessionState" && e.Attribute("mode")?.Value is not "Off")
                add(Rules.SessionState, file, Line(e), $"<sessionState mode=\"{e.Attribute("mode")?.Value ?? "InProc"}\">");
            else if (n == "system.serviceModel" && e.HasElements)
                add(Rules.WcfConfig, file, Line(e), "<system.serviceModel>");
            else if (n == "machineKey")
                add(Rules.MachineKey, file, Line(e), "<machineKey …> (values redacted)");
            else if (n == "add" && parent == "connectionStrings" && e.Attribute("connectionString")?.Value is { } cs && HasSecret(cs))
                add(Rules.PlaintextSecret, file, Line(e), $"connection string \"{e.Attribute("name")?.Value}\" contains a password (redacted)");
            else if (n == "add" && parent == "appSettings" && e.Attribute("key")?.Value is { } key && SecretKey().IsMatch(key) && IsRealValue(e.Attribute("value")?.Value))
                add(Rules.PlaintextSecret, file, Line(e), $"appSetting \"{key}\" holds a secret (redacted)");
        }
    }

    private static bool HasSecret(string connectionString)
    {
        var m = PasswordPart().Match(connectionString);
        return m.Success && IsRealValue(m.Groups[1].Value);
    }

    private static bool IsRealValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;
        var v = value.Trim();
        return !(v.StartsWith("$(", StringComparison.Ordinal) || v.StartsWith("#{", StringComparison.Ordinal) || v.StartsWith("{{", StringComparison.Ordinal)
                 || v.StartsWith("__", StringComparison.Ordinal) || v.StartsWith('%') || v.Equals("changeme", StringComparison.OrdinalIgnoreCase));
    }

    private static XDocument LoadXml(string file)
    {
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null };
        using var reader = XmlReader.Create(file, settings);
        return XDocument.Load(reader, LoadOptions.SetLineInfo);
    }

    private static int LineOf(string text, int index)
    {
        var line = 1;
        for (var i = 0; i < index; i++) if (text[i] == '\n') line++;
        return line;
    }

    private static string Trim(string evidence)
    {
        var e = evidence.Trim().Replace('\t', ' ');
        return e.Length > 160 ? string.Concat(e.AsSpan(0, 157), "…") : e;
    }

    [GeneratedRegex(@"(?:^|;)\s*(?:password|pwd)\s*=\s*([^;]*)", RegexOptions.IgnoreCase)]
    private static partial Regex PasswordPart();

    [GeneratedRegex(@"password|passwd|secret|api[_-]?key|token|connectionstring", RegexOptions.IgnoreCase)]
    private static partial Regex SecretKey();
}
