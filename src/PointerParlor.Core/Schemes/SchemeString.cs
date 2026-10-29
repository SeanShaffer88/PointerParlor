namespace PointerParlor.Core.Schemes;

/// <summary>
/// The comma-separated list of cursor paths Windows stores for a scheme, one entry per role in
/// <see cref="CursorRole"/> order. Older schemes carry fewer entries; missing ones mean "Windows default".
/// </summary>
public static class SchemeString
{
    public static string Format(IReadOnlyDictionary<CursorRole, string> paths)
    {
        var parts = new string[CursorRoles.All.Count];
        foreach (var info in CursorRoles.All)
        {
            var path = paths.GetValueOrDefault(info.Role) ?? "";
            if (path.Contains(','))
                throw new ArgumentException($"Cursor paths in a scheme cannot contain commas: {path}", nameof(paths));
            parts[(int)info.Role] = path;
        }
        return string.Join(',', parts);
    }

    public static Dictionary<CursorRole, string> Parse(string scheme)
    {
        var result = new Dictionary<CursorRole, string>();
        var parts = scheme.Split(',');
        for (var i = 0; i < parts.Length && i < CursorRoles.All.Count; i++)
        {
            var path = parts[i].Trim();
            if (path.Length > 0)
                result[(CursorRole)i] = path;
        }
        return result;
    }
}
