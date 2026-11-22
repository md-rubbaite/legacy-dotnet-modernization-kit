using System.Globalization;
using Modernize;

return Cli.Run(args, Console.Out, Console.Error);

internal static class Cli
{
    private const string Usage = """
        modernize scan <path> [--format md|json|sarif] [--out <file>] [--fail-on blocker|high|medium|low|none] [--as-of yyyy-MM-dd]
        modernize rules

        Exit codes: 0 no findings at or above --fail-on (default: blocker), 1 findings at or above it, 2 usage error.
        """;

    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length == 1 && args[0] == "rules")
        {
            stdout.Write(Reports.RulesMarkdown());
            return 0;
        }
        if (args.Length < 2 || args[0] != "scan")
        {
            stderr.WriteLine(Usage);
            return 2;
        }
        var path = args[1];
        var format = "md";
        string? output = null;
        Severity? failOn = Severity.Blocker;
        var asOf = DateOnly.FromDateTime(DateTime.UtcNow);
        for (var i = 2; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : null;
            switch (args[i])
            {
                case "--format" when value is "md" or "json" or "sarif":
                    format = value;
                    i++;
                    break;
                case "--out" when value is not null:
                    output = value;
                    i++;
                    break;
                case "--fail-on" when value is "none":
                    failOn = null;
                    i++;
                    break;
                case "--fail-on" when Enum.TryParse<Severity>(value, ignoreCase: true, out var s) && !int.TryParse(value, out _):
                    failOn = s;
                    i++;
                    break;
                case "--as-of" when DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d):
                    asOf = d;
                    i++;
                    break;
                default:
                    stderr.WriteLine($"unknown or invalid option: {args[i]}");
                    stderr.WriteLine(Usage);
                    return 2;
            }
        }
        if (!Directory.Exists(path))
        {
            stderr.WriteLine($"not a directory: {path}");
            return 2;
        }

        var result = Scanner.Scan(path, new ScanOptions { AsOf = asOf });
        var report = format switch
        {
            "json" => Reports.JsonReport(result),
            "sarif" => Reports.Sarif(result),
            _ => Reports.Markdown(result),
        };
        if (output is null) stdout.Write(report);
        else File.WriteAllText(output, report);

        stderr.WriteLine($"{result.Projects.Count} projects, {result.Findings.Count} findings ({result.Count(Severity.Blocker)} blocker)");
        return failOn is { } level && result.Count(level) > 0 ? 1 : 0;
    }
}
