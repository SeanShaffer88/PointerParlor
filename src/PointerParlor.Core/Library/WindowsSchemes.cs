using PointerParlor.Core.Schemes;

namespace PointerParlor.Core.Library;

/// <summary>Turns the schemes that ship with Windows (HKLM) into read-only gallery sets.</summary>
public static class WindowsSchemes
{
    public const string IdPrefix = "windows-";
    public const string DefaultSchemeName = "Windows Default";

    /// <param name="schemes">Scheme name to its scheme string, as read from the registry.</param>
    /// <param name="expand">Expands environment variables such as %SystemRoot%.</param>
    public static IReadOnlyList<CursorSet> FromRegistry(IReadOnlyDictionary<string, string> schemes, Func<string, string> expand)
    {
        var sets = new List<CursorSet>();
        foreach (var (name, scheme) in schemes.OrderBy(s => s.Key == DefaultSchemeName ? 0 : 1).ThenBy(s => s.Key, StringComparer.OrdinalIgnoreCase))
        {
            var cursors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (role, path) in SchemeString.Parse(scheme))
                cursors[CursorRoles.Info(role).RegistryName] = expand(path);
            if (cursors.Count == 0)
                continue;
            var manifest = new SetManifest
            {
                Id = IdPrefix + CursorLibrary.Slug(name),
                Name = name,
                Kind = SetKind.Windows,
                Author = "Microsoft",
                Cursors = cursors,
            };
            sets.Add(new CursorSet(manifest, null));
        }
        return sets;
    }
}
