using System.Text;
using System.Text.RegularExpressions;
using PointerParlor.Core.Schemes;

namespace PointerParlor.Core.Import;

/// <summary>What a cursor-set install .inf says: the scheme's name and which file each role uses.</summary>
public sealed record InfScheme(string? Name, IReadOnlyDictionary<CursorRole, string> Files);

/// <summary>
/// Reads cursor-set install files. Most packs register a scheme line such as
/// <c>HKCU,"Control Panel\Cursors\Schemes","%SCHEME_NAME%",,"%10%\%CUR_DIR%\%pointer%,..."</c>
/// with the names defined in [Strings]; some instead write each role under Control Panel\Cursors.
/// Only the file name of each path is kept, since the files are imported from beside the .inf.
/// </summary>
public static partial class InfParser
{
    public static InfScheme Parse(string text)
    {
        var sections = ReadSections(text);
        var strings = sections.TryGetValue("strings", out var s) ? ReadStrings(s) : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        string? name = null;
        var files = new Dictionary<CursorRole, string>();

        foreach (var line in sections.Values.SelectMany(l => l))
        {
            var fields = SplitFields(line);
            if (fields.Count < 5 || !fields[0].Equals("HKCU", StringComparison.OrdinalIgnoreCase))
                continue;
            var key = Expand(fields[1], strings);
            var valueName = Expand(fields[2], strings);
            var value = Expand(string.Join(",", fields.Skip(4)), strings);

            if (key.Equals(RegistryPaths.UserSchemes, StringComparison.OrdinalIgnoreCase))
            {
                name ??= valueName.Length > 0 ? valueName : null;
                foreach (var (role, path) in SchemeString.Parse(value))
                    files.TryAdd(role, FileNameOf(path));
            }
            else if (key.Equals(RegistryPaths.Cursors, StringComparison.OrdinalIgnoreCase)
                     && CursorRoles.TryParseRegistryName(valueName, out var role) && value.Length > 0)
            {
                files.TryAdd(role, FileNameOf(value));
            }
        }

        if (name is null && strings.TryGetValue("SCHEME_NAME", out var schemeName) && schemeName.Length > 0)
            name = schemeName;
        return new InfScheme(name, files);
    }

    private static Dictionary<string, List<string>> ReadSections(string text)
    {
        var sections = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        List<string>? current = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = StripComment(raw).Trim();
            if (line.Length == 0)
                continue;
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                var section = line[1..^1].Trim();
                if (!sections.TryGetValue(section, out current))
                    sections[section] = current = [];
                continue;
            }
            current?.Add(line);
        }
        return sections;
    }

    private static Dictionary<string, string> ReadStrings(List<string> lines)
    {
        var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines)
        {
            var eq = line.IndexOf('=');
            if (eq <= 0)
                continue;
            strings[Unquote(line[..eq].Trim())] = Unquote(line[(eq + 1)..].Trim());
        }
        return strings;
    }

    /// <summary>Splits an INF line on commas that are outside double quotes, unquoting each field.</summary>
    private static List<string> SplitFields(string line)
    {
        var fields = new List<string>();
        var sb = new StringBuilder();
        var inQuotes = false;
        foreach (var c in line)
        {
            if (c == '"')
                inQuotes = !inQuotes;
            else if (c == ',' && !inQuotes)
            {
                fields.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
                sb.Append(c);
        }
        fields.Add(sb.ToString().Trim());
        return fields;
    }

    private static string Expand(string value, Dictionary<string, string> strings)
    {
        // A couple of passes covers strings that refer to other strings.
        for (var pass = 0; pass < 3 && value.Contains('%'); pass++)
            value = TokenPattern().Replace(value, m => strings.TryGetValue(m.Groups[1].Value, out var v) ? v : m.Value);
        return value.Trim();
    }

    private static string FileNameOf(string path)
    {
        var trimmed = path.Trim().Trim('"');
        var slash = trimmed.LastIndexOfAny(['\\', '/']);
        return slash >= 0 ? trimmed[(slash + 1)..] : trimmed;
    }

    private static string StripComment(string line)
    {
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            if (line[i] == '"')
                inQuotes = !inQuotes;
            else if (line[i] == ';' && !inQuotes)
                return line[..i];
        }
        return line;
    }

    private static string Unquote(string s) => s.Length >= 2 && s[0] == '"' && s[^1] == '"' ? s[1..^1] : s;

    [GeneratedRegex("%([^%]+)%")]
    private static partial Regex TokenPattern();
}
