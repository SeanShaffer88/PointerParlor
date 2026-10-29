namespace PointerParlor.Core.Library;

public enum ActiveKind
{
    /// <summary>Every pointer is the Windows default (all values empty).</summary>
    WindowsDefault,

    /// <summary>The pointers exactly match one set.</summary>
    Set,

    /// <summary>The pointers come from more than one place.</summary>
    Mix,
}

/// <summary>One pointer as it is set right now, and the set it belongs to, if any.</summary>
public sealed record ActiveCursor(CursorRole Role, string? Path, CursorSet? FromSet);

public sealed record ActiveCursorState(ActiveKind Kind, CursorSet? Set, IReadOnlyList<ActiveCursor> Cursors)
{
    /// <summary>The paths to copy when saving the current mix. Roles left at the Windows default are skipped.</summary>
    public IReadOnlyDictionary<CursorRole, string> MixPaths =>
        Cursors.Where(c => c.Path is not null).ToDictionary(c => c.Role, c => c.Path!);
}

/// <summary>Works out what is applied right now by matching the registry's cursor paths against the library.</summary>
public static class ActiveState
{
    /// <param name="current">Role to the raw path in HKCU\Control Panel\Cursors (may contain %variables%).</param>
    /// <param name="sets">Every set the gallery knows, library and Windows schemes alike.</param>
    /// <param name="expand">Expands environment variables.</param>
    public static ActiveCursorState Detect(IReadOnlyDictionary<CursorRole, string> current, IReadOnlyList<CursorSet> sets, Func<string, string> expand)
    {
        var normalizedCurrent = CursorRoles.All.ToDictionary(r => r.Role, r => Normalize(current.GetValueOrDefault(r.Role), expand));

        if (normalizedCurrent.Values.All(v => v.Length == 0))
            return new ActiveCursorState(ActiveKind.WindowsDefault, null, Cursors(current, normalizedCurrent, sets, expand, null));

        foreach (var set in sets)
        {
            var matches = CursorRoles.All.All(r =>
                normalizedCurrent[r.Role] == Normalize(set.Paths.GetValueOrDefault(r.Role), expand));
            if (matches)
                return new ActiveCursorState(ActiveKind.Set, set, Cursors(current, normalizedCurrent, sets, expand, set));
        }
        return new ActiveCursorState(ActiveKind.Mix, null, Cursors(current, normalizedCurrent, sets, expand, null));
    }

    private static List<ActiveCursor> Cursors(IReadOnlyDictionary<CursorRole, string> current, Dictionary<CursorRole, string> normalized,
        IReadOnlyList<CursorSet> sets, Func<string, string> expand, CursorSet? matched)
    {
        var list = new List<ActiveCursor>();
        foreach (var info in CursorRoles.All)
        {
            var path = normalized[info.Role].Length == 0 ? null : expand(current[info.Role].Trim());
            var owner = matched;
            if (owner is null && path is not null)
            {
                // Prefer the set that owns this file for this same role, then any set holding the file.
                owner = sets.FirstOrDefault(s => Normalize(s.Paths.GetValueOrDefault(info.Role), expand) == normalized[info.Role])
                        ?? sets.FirstOrDefault(s => s.Paths.Values.Any(p => Normalize(p, expand) == normalized[info.Role]));
            }
            list.Add(new ActiveCursor(info.Role, path, path is null ? null : owner));
        }
        return list;
    }

    /// <summary>Paths compare case-insensitively with either slash, after expanding variables.</summary>
    public static string Normalize(string? path, Func<string, string> expand) =>
        string.IsNullOrWhiteSpace(path) ? "" : expand(path.Trim()).Replace('/', '\\').ToUpperInvariant();
}
