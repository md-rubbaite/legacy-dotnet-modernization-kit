namespace Modernize.Tests;

public class SanitizerTests
{
    [Fact]
    public void Blanks_comments_and_literals_but_keeps_code_and_line_breaks()
    {
        var src = "var a = \"BinaryFormatter\"; // HttpContext.Current\n/* new WebClient()\n */ var b = new BinaryFormatter();\nvar c = @\"x \"\" y\"; var d = $\"{e}\"; var f = '\\'';\nvar g = \"\"\"\n  raw \" text\n  \"\"\"; Thread.Abort();";
        var clean = CSharpSanitizer.Sanitize(src);
        Assert.Equal(src.Length, clean.Length);
        Assert.Equal(src.Split('\n').Length, clean.Split('\n').Length);
        Assert.DoesNotContain("HttpContext", clean);
        Assert.DoesNotContain("WebClient", clean);
        Assert.DoesNotContain("raw", clean);
        Assert.Contains("new BinaryFormatter()", clean);
        Assert.Contains("Thread.Abort()", clean);
        Assert.Equal(1, clean.Split("BinaryFormatter").Length - 1);
    }

    [Fact]
    public void Unterminated_string_stops_at_end_of_line()
    {
        var clean = CSharpSanitizer.Sanitize("var s = \"oops\nvar f = new BinaryFormatter();");
        Assert.Contains("new BinaryFormatter()", clean);
    }
}

public class SupportPolicyTests
{
    [Theory]
    [InlineData("v4.7.2", "net472")]
    [InlineData("v4.8", "net48")]
    [InlineData("net8.0-windows", "net8.0")]
    [InlineData("NET10.0", "net10.0")]
    [InlineData("netstandard2.0", "netstandard2.0")]
    public void Normalizes_framework_names(string input, string expected) => Assert.Equal(expected, SupportPolicy.Normalize(input));

    [Fact]
    public void Knows_framework_families_and_dates()
    {
        Assert.True(SupportPolicy.IsNetFramework("v4.6.1"));
        Assert.False(SupportPolicy.IsNetFramework("net8.0"));
        Assert.False(SupportPolicy.IsNetFramework("netstandard2.0"));
        Assert.Equal(new DateOnly(2026, 11, 10), SupportPolicy.EndOf("net8.0"));
        Assert.Equal(new DateOnly(2027, 1, 12), SupportPolicy.EndOf("net462"));
        Assert.Null(SupportPolicy.EndOf("net48"));
        Assert.Equal("out of support since 2026-11-10", SupportPolicy.Describe("net9.0", new DateOnly(2026, 11, 22)));
        Assert.Equal("supported until 2028-11-14", SupportPolicy.Describe("net10.0", new DateOnly(2026, 11, 22)));
    }

    [Fact]
    public void Every_rule_has_a_unique_id_and_guidance()
    {
        Assert.Equal(Rules.All.Count, Rules.All.Select(r => r.Id).Distinct().Count());
        Assert.All(Rules.All, r => Assert.True(r.Guidance.Length > 20, r.Id));
    }
}

public class CliTests
{
    private static (int Code, string Out, string Err) Run(params string[] args)
    {
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var code = Cli.Run(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    [Fact]
    public void Exit_codes_follow_the_fail_on_threshold()
    {
        var shop = Paths.LegacyShop;
        Assert.Equal(1, Run("scan", shop, "--as-of", "2026-11-22").Code);
        Assert.Equal(0, Run("scan", shop, "--fail-on", "none").Code);
        Assert.Equal(2, Run("scan", shop, "--format", "pdf").Code);
        Assert.Equal(2, Run("scan", Path.Combine(shop, "missing")).Code);
        Assert.Equal(2, Run().Code);
    }

    [Fact]
    public void Writes_the_requested_format_to_a_file()
    {
        var file = Path.GetTempFileName();
        try
        {
            var (code, stdout, _) = Run("scan", Paths.LegacyShop, "--format", "sarif", "--out", file, "--fail-on", "none");
            Assert.Equal(0, code);
            Assert.Equal("", stdout);
            Assert.Contains("\"version\": \"2.1.0\"", File.ReadAllText(file));
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public void Lists_the_rules()
    {
        var (code, stdout, _) = Run("rules");
        Assert.Equal(0, code);
        Assert.Contains("LDM120", stdout);
    }
}
