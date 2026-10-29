namespace PointerParlor.Core.Schemes;

/// <summary>
/// Works out the registry writes for each cursor action. It never touches the registry itself:
/// the app shell applies the returned changes and then asks Windows to reload its cursors.
/// </summary>
public static class SchemePlanner
{
    public const string SchemePrefix = "PointerParlor - ";
    public const int MinPointerSize = 32;
    public const int MaxPointerSize = 256;

    // "Scheme Source" values Windows uses: 0 = no scheme, 1 = user scheme, 2 = system scheme.
    private const int SourceNone = 0;
    private const int SourceUser = 1;
    private const int SourceSystem = 2;

    public static string SchemeNameFor(string setName) => SchemePrefix + setName.Replace(',', ' ').Trim();

    /// <summary>
    /// Applies every role of a set and registers it as a user scheme. Roles the set leaves out are
    /// cleared, which makes Windows use its default cursor for them.
    /// </summary>
    public static IReadOnlyList<RegistryChange> ApplySet(string setName, IReadOnlyDictionary<CursorRole, string> paths)
    {
        var schemeName = SchemeNameFor(setName);
        var changes = RoleWrites(paths);
        changes.Add(new(RegistryPaths.Cursors, "", RegValue.Str(schemeName)));
        changes.Add(new(RegistryPaths.Cursors, RegistryPaths.SchemeSourceValue, RegValue.DWord(SourceUser)));
        changes.Add(new(RegistryPaths.UserSchemes, schemeName, RegValue.Expand(SchemeString.Format(paths))));
        return changes;
    }

    /// <summary>Applies one of the schemes that ship with Windows. Those are already registered under HKLM.</summary>
    public static IReadOnlyList<RegistryChange> ApplySystemScheme(string schemeName, IReadOnlyDictionary<CursorRole, string> paths)
    {
        var changes = RoleWrites(paths);
        changes.Add(new(RegistryPaths.Cursors, "", RegValue.Str(schemeName)));
        changes.Add(new(RegistryPaths.Cursors, RegistryPaths.SchemeSourceValue, RegValue.DWord(SourceSystem)));
        return changes;
    }

    /// <summary>Swaps a single pointer. The active scheme name is cleared because the cursors no longer match it.</summary>
    public static IReadOnlyList<RegistryChange> ApplySingle(CursorRole role, string path) =>
    [
        new(RegistryPaths.Cursors, CursorRoles.Info(role).RegistryName, RegValue.Expand(path)),
        new(RegistryPaths.Cursors, "", RegValue.Str("")),
    ];

    /// <summary>
    /// Restores the Windows default cursors: the "Windows Default" system scheme when its paths are
    /// known, otherwise empty values, which Windows also reads as its defaults.
    /// </summary>
    public static IReadOnlyList<RegistryChange> ResetToDefault(IReadOnlyDictionary<CursorRole, string>? windowsDefaultPaths)
    {
        if (windowsDefaultPaths is { Count: > 0 })
            return ApplySystemScheme("Windows Default", windowsDefaultPaths);

        var changes = RoleWrites(new Dictionary<CursorRole, string>());
        changes.Add(new(RegistryPaths.Cursors, "", RegValue.Str("")));
        changes.Add(new(RegistryPaths.Cursors, RegistryPaths.SchemeSourceValue, RegValue.DWord(SourceNone)));
        return changes;
    }

    /// <summary>Sets the pointer size Windows uses (the same value as the Settings app's size slider).</summary>
    public static IReadOnlyList<RegistryChange> SetPointerSize(int pixels)
    {
        if (pixels is < MinPointerSize or > MaxPointerSize)
            throw new ArgumentOutOfRangeException(nameof(pixels), $"Pointer size must be between {MinPointerSize} and {MaxPointerSize} pixels.");
        return [new(RegistryPaths.Cursors, RegistryPaths.BaseSizeValue, RegValue.DWord(pixels))];
    }

    /// <summary>Removes a set's registered scheme, for example when the set is deleted.</summary>
    public static IReadOnlyList<RegistryChange> RemoveScheme(string setName) =>
        [new(RegistryPaths.UserSchemes, SchemeNameFor(setName), null)];

    private static List<RegistryChange> RoleWrites(IReadOnlyDictionary<CursorRole, string> paths) =>
        CursorRoles.All
            .Select(info => new RegistryChange(RegistryPaths.Cursors, info.RegistryName, RegValue.Expand(paths.GetValueOrDefault(info.Role) ?? "")))
            .ToList();
}
