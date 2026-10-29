using System.Text.Json.Serialization;

namespace PointerParlor.Core.Library;

/// <summary>Where a set came from. It decides the gallery group and whether the set can be changed.</summary>
public enum SetKind
{
    /// <summary>PointerParlor's own sets, shipped with the app.</summary>
    Signature,

    /// <summary>Openly licensed sets shipped with the app, credited in the set and in the notices.</summary>
    OpenLicense,

    /// <summary>The schemes that come with Windows. Read from the registry, never copied.</summary>
    Windows,

    /// <summary>Sets the user imported from files.</summary>
    Imported,

    /// <summary>Mixes the user saved.</summary>
    Mine,
}

/// <summary>The set.json stored beside a set's cursor files.</summary>
public sealed record SetManifest
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required SetKind Kind { get; init; }
    public string? Author { get; init; }
    public string? License { get; init; }
    public string? SourceUrl { get; init; }

    /// <summary>One sentence describing the set's look, shown on its page.</summary>
    public string? Description { get; init; }

    /// <summary>Bundled sets carry a version so a newer app can refresh the copy in the library.</summary>
    public int Version { get; init; } = 1;

    public DateTimeOffset CreatedUtc { get; init; }

    /// <summary>Role registry name (for example "Arrow") to a file name in the set's folder, or an absolute path for Windows sets.</summary>
    public Dictionary<string, string> Cursors { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>A set in the library, with its files resolved to absolute paths.</summary>
public sealed class CursorSet
{
    public CursorSet(SetManifest manifest, string? directory)
    {
        Manifest = manifest;
        Directory = directory;
        var paths = new Dictionary<CursorRole, string>();
        foreach (var (roleName, file) in manifest.Cursors)
        {
            if (!CursorRoles.TryParseRegistryName(roleName, out var role) || string.IsNullOrWhiteSpace(file))
                continue;
            paths[role] = directory is null ? file : System.IO.Path.Combine(directory, file);
        }
        Paths = paths;
    }

    public SetManifest Manifest { get; }

    /// <summary>The set's folder in the library; null for Windows schemes.</summary>
    public string? Directory { get; }

    public string Id => Manifest.Id;
    public string Name => Manifest.Name;
    public SetKind Kind => Manifest.Kind;

    [JsonIgnore]
    public IReadOnlyDictionary<CursorRole, string> Paths { get; }

    /// <summary>Bundled sets and Windows schemes cannot be renamed or deleted.</summary>
    public bool IsReadOnly => Kind is SetKind.Signature or SetKind.OpenLicense or SetKind.Windows;

    public bool IsComplete => Paths.Count == CursorRoles.All.Count;
}
