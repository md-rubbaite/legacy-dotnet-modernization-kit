using System.Text;

namespace Modernize;

/// <summary>
/// Blanks out comments and the contents of string and character literals, keeping every newline,
/// so pattern rules match code only and report the right line numbers.
/// </summary>
internal static class CSharpSanitizer
{
    public static string Sanitize(string source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var sb = new StringBuilder(source.Length);
        var i = 0;
        while (i < source.Length)
        {
            var c = source[i];
            var next = i + 1 < source.Length ? source[i + 1] : '\0';
            if (c == '/' && next == '/')
            {
                while (i < source.Length && source[i] != '\n') { sb.Append(' '); i++; }
            }
            else if (c == '/' && next == '*')
            {
                sb.Append("  ");
                i += 2;
                while (i < source.Length && !(source[i] == '*' && i + 1 < source.Length && source[i + 1] == '/')) Blank(sb, source[i++]);
                if (i < source.Length) { sb.Append("  "); i += 2; }
            }
            else if (c == '"' && next == '"' && i + 2 < source.Length && source[i + 2] == '"')
            {
                i = RawString(source, i, sb);
            }
            else if (c == '"' || ((c == '@' || c == '$') && IsStringStart(source, i)))
            {
                i = QuotedString(source, i, sb);
            }
            else if (c == '\'')
            {
                sb.Append('\'');
                i++;
                while (i < source.Length && source[i] != '\'' && source[i] != '\n')
                {
                    if (source[i] == '\\' && i + 1 < source.Length) { sb.Append(' '); i++; }
                    Blank(sb, source[i++]);
                }
                if (i < source.Length && source[i] == '\'') { sb.Append('\''); i++; }
            }
            else
            {
                sb.Append(c);
                i++;
            }
        }
        return sb.ToString();
    }

    private static bool IsStringStart(string s, int i)
    {
        var j = i;
        while (j < s.Length && (s[j] == '@' || s[j] == '$')) j++;
        return j < s.Length && s[j] == '"' && j - i <= 3;
    }

    private static int QuotedString(string s, int i, StringBuilder sb)
    {
        var verbatim = false;
        while (s[i] != '"')
        {
            if (s[i] == '@') verbatim = true;
            sb.Append(s[i++]);
        }
        if (i + 2 < s.Length && s[i + 1] == '"' && s[i + 2] == '"') return RawString(s, i, sb);
        sb.Append('"');
        i++;
        while (i < s.Length)
        {
            var c = s[i];
            if (verbatim && c == '"' && i + 1 < s.Length && s[i + 1] == '"') { sb.Append("  "); i += 2; continue; }
            if (!verbatim && c == '\\' && i + 1 < s.Length) { sb.Append(' '); Blank(sb, s[i + 1]); i += 2; continue; }
            if (c == '"') { sb.Append('"'); return i + 1; }
            if (!verbatim && c == '\n') return i; // unterminated: stop at end of line
            Blank(sb, c);
            i++;
        }
        return i;
    }

    private static int RawString(string s, int i, StringBuilder sb)
    {
        var quotes = 0;
        while (i < s.Length && s[i] == '"') { quotes++; sb.Append('"'); i++; }
        while (i < s.Length)
        {
            if (s[i] == '"')
            {
                var run = 0;
                while (i + run < s.Length && s[i + run] == '"') run++;
                if (run >= quotes)
                {
                    sb.Append('"', run);
                    return i + run;
                }
                sb.Append(' ', run);
                i += run;
                continue;
            }
            Blank(sb, s[i++]);
        }
        return i;
    }

    private static void Blank(StringBuilder sb, char c) => sb.Append(c is '\n' or '\r' ? c : ' ');
}
