using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PointerParlor.Core.Library;

/// <summary>
/// The cursor sets on disk: one folder per set holding set.json and the cursor files.
/// Windows reads the cursor files from these folders directly, so files are never moved once applied.
/// </summary>
public sealed class CursorLibrary
{
    public const string ManifestFileName = "set.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public CursorLibrary(string rootDirectory)
    {
        RootDirectory = rootDirectory;
    }

    public string RootDirectory { get; }

    /// <summary>Loads every set. Folders with a missing or broken set.json are skipped and reported.</summary>
    public (IReadOnlyList<CursorSet> Sets, IReadOnlyList<string> Problems) LoadAll()
    {
        var sets = new List<CursorSet>();
        var problems = new List<string>();
        if (!System.IO.Directory.Exists(RootDirectory))
            return (sets, problems);

        foreach (var dir in System.IO.Directory.EnumerateDirectories(RootDirectory).Order(StringComparer.OrdinalIgnoreCase))
        {
            var manifestPath = Path.Combine(dir, ManifestFileName);
            if (!File.Exists(manifestPath))
                continue;
            var manifest = ReadManifest(manifestPath, out var error);
            if (manifest is null)
                problems.Add($"{Path.GetFileName(dir)}: {error}");
            else
                sets.Add(new CursorSet(manifest, dir));
        }
        return (sets, problems);
    }

    public static SetManifest? ReadManifest(string path, out string? error)
    {
        error = null;
        try
        {
            var manifest = JsonSerializer.Deserialize<SetManifest>(File.ReadAllText(path), JsonOptions);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id) || string.IsNullOrWhiteSpace(manifest.Name))
            {
                error = "set.json is missing the set's id or name.";
                return null;
            }
            return manifest with { Cursors = new Dictionary<string, string>(manifest.Cursors, StringComparer.OrdinalIgnoreCase) };
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            error = $"set.json could not be read ({ex.Message}).";
            return null;
        }
    }

    /// <summary>
    /// Creates a new set by copying each role's source file into a fresh folder. Files are renamed to
    /// their role (for example Arrow.cur), which keeps paths free of commas, as scheme strings require.
    /// </summary>
    public CursorSet Create(string name, SetKind kind, IReadOnlyDictionary<CursorRole, string> sourceFiles, DateTimeOffset now,
        string? author = null, string? license = null, string? sourceUrl = null)
    {
        if (kind is not (SetKind.Imported or SetKind.Mine))
            throw new ArgumentException("Only imported sets and saved mixes are created in the library.", nameof(kind));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("A set needs a name.", nameof(name));
        if (sourceFiles.Count == 0)
            throw new ArgumentException("A set needs at least one cursor.", nameof(sourceFiles));

        var id = NewId(name);
        var dir = Path.Combine(RootDirectory, id);
        System.IO.Directory.CreateDirectory(dir);
        try
        {
            var cursors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (role, source) in sourceFiles)
            {
                var registryName = CursorRoles.Info(role).RegistryName;
                var fileName = registryName + Path.GetExtension(source).ToLowerInvariant();
                File.Copy(source, Path.Combine(dir, fileName));
                cursors[registryName] = fileName;
            }

            var manifest = new SetManifest
            {
                Id = id,
                Name = name.Trim(),
                Kind = kind,
                Author = author,
                License = license,
                SourceUrl = sourceUrl,
                CreatedUtc = now,
                Cursors = cursors,
            };
            WriteManifest(dir, manifest);
            return new CursorSet(manifest, dir);
        }
        catch
        {
            TryDeleteDirectory(dir);
            throw;
        }
    }

    public CursorSet Rename(CursorSet set, string newName)
    {
        if (set.IsReadOnly || set.Directory is null)
            throw new InvalidOperationException($"\"{set.Name}\" comes with PointerParlor or Windows and cannot be renamed.");
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("A set needs a name.", nameof(newName));
        var manifest = set.Manifest with { Name = newName.Trim() };
        WriteManifest(set.Directory, manifest);
        return new CursorSet(manifest, set.Directory);
    }

    public void Delete(CursorSet set)
    {
        if (set.IsReadOnly || set.Directory is null)
            throw new InvalidOperationException($"\"{set.Name}\" comes with PointerParlor or Windows and cannot be deleted.");
        System.IO.Directory.Delete(set.Directory, recursive: true);
    }

    /// <summary>
    /// Copies sets shipped beside the app into the library, so the paths Windows stores stay valid when
    /// the app is updated or moved. A set is refreshed only when the shipped copy has a newer version.
    /// </summary>
    public IReadOnlyList<string> InstallBundled(string bundledRoot)
    {
        var installed = new List<string>();
        if (!System.IO.Directory.Exists(bundledRoot))
            return installed;

        foreach (var src in System.IO.Directory.EnumerateDirectories(bundledRoot))
        {
            var manifest = ReadManifest(Path.Combine(src, ManifestFileName), out _);
            if (manifest is null || manifest.Kind is not (SetKind.Signature or SetKind.OpenLicense))
                continue;
            var dest = Path.Combine(RootDirectory, manifest.Id);
            var existing = ReadManifest(Path.Combine(dest, ManifestFileName), out _);
            if (existing is not null && existing.Version >= manifest.Version)
                continue;

            System.IO.Directory.CreateDirectory(dest);
            foreach (var file in System.IO.Directory.EnumerateFiles(src))
                File.Copy(file, Path.Combine(dest, Path.GetFileName(file)), overwrite: true);
            installed.Add(manifest.Id);
        }
        return installed;
    }

    private static void WriteManifest(string dir, SetManifest manifest)
    {
        var path = Path.Combine(dir, ManifestFileName);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(manifest, JsonOptions));
        File.Move(temp, path, overwrite: true);
    }

    private string NewId(string name)
    {
        var slug = Slug(name);
        for (var attempt = 0; ; attempt++)
        {
            var id = attempt == 0 ? slug : $"{slug}-{attempt + 1}";
            if (!System.IO.Directory.Exists(Path.Combine(RootDirectory, id)))
                return id;
        }
    }

    /// <summary>A folder-safe, comma-free id made from a set name.</summary>
    public static string Slug(string name)
    {
        var sb = new StringBuilder();
        foreach (var c in name.Trim().ToLowerInvariant())
        {
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
                sb.Append(c);
            else if (sb.Length > 0 && sb[^1] != '-')
                sb.Append('-');
        }
        var slug = sb.ToString().Trim('-');
        if (slug.Length > 40)
            slug = slug[..40].TrimEnd('-');
        return slug.Length == 0 ? "set" : slug;
    }

    private static void TryDeleteDirectory(string dir)
    {
        try { System.IO.Directory.Delete(dir, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
