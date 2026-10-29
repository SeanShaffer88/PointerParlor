using System.IO.Compression;
using PointerParlor.Core.Imaging;
using PointerParlor.Core.Library;

namespace PointerParlor.Core.Import;

/// <summary>
/// What an import would create, shown to the user before anything is copied. Role assignments
/// can be changed before <see cref="SetImporter.Commit"/>. Disposing removes any extracted .zip.
/// </summary>
public sealed class ImportPlan : IDisposable
{
    private readonly string? _tempDirectory;

    internal ImportPlan(string suggestedName, Dictionary<CursorRole, string> assignments, List<string> unassigned,
        List<string> warnings, string? tempDirectory, string? author)
    {
        SuggestedName = suggestedName;
        Assignments = assignments;
        Unassigned = unassigned;
        Warnings = warnings;
        _tempDirectory = tempDirectory;
        Author = author;
    }

    public string SuggestedName { get; }
    public string? Author { get; }

    /// <summary>Role to the source file that will be copied for it.</summary>
    public Dictionary<CursorRole, string> Assignments { get; }

    /// <summary>Readable cursor files whose role could not be worked out.</summary>
    public List<string> Unassigned { get; }

    /// <summary>Plain-language notes, for example files that could not be read.</summary>
    public List<string> Warnings { get; }

    public void Dispose()
    {
        if (_tempDirectory is null)
            return;
        try { Directory.Delete(_tempDirectory, recursive: true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}

/// <summary>Imports cursor sets from an .inf, a folder, a .zip, or one or more .cur/.ani files.</summary>
public sealed class SetImporter(string tempRoot)
{
    public const long MaxZipBytes = 200L * 1024 * 1024;
    public const int MaxZipEntries = 2000;
    private static readonly string[] CursorExtensions = [".cur", ".ani"];

    public static bool IsCursorFile(string path) => CursorExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    /// <summary>Plans an import from a single path: a folder, .zip, .inf, .cur or .ani.</summary>
    public ImportPlan Plan(string path)
    {
        if (Directory.Exists(path))
            return PlanFolder(path, Path.GetFileName(Path.TrimEndingDirectorySeparator(path)), null);

        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext switch
        {
            ".zip" => PlanZip(path),
            ".inf" => PlanFolder(Path.GetDirectoryName(Path.GetFullPath(path))!, Path.GetFileNameWithoutExtension(path), null, path),
            ".cur" or ".ani" => PlanFiles([path]),
            _ => throw new InvalidOperationException($"PointerParlor can't import {Path.GetFileName(path)}. Choose a .cur, .ani, .inf or .zip file, or a folder."),
        };
    }

    /// <summary>Plans one set from loose cursor files, named after the first file's folder.</summary>
    public ImportPlan PlanFiles(IReadOnlyList<string> files)
    {
        var warnings = new List<string>();
        var readable = files.Where(f => IsReadable(f, warnings)).ToList();
        var (assignments, unassigned) = AssignByName(readable);
        // A single file with an unguessable name still deserves a home: make it the normal pointer.
        if (readable.Count == 1 && assignments.Count == 0)
        {
            assignments[CursorRole.Arrow] = readable[0];
            unassigned.Clear();
        }
        var name = files.Count == 1
            ? Path.GetFileNameWithoutExtension(files[0])
            : Path.GetFileName(Path.GetDirectoryName(Path.GetFullPath(files[0]))) ?? "Imported cursors";
        return new ImportPlan(name, assignments, unassigned, warnings, null, null);
    }

    public CursorSet Commit(ImportPlan plan, CursorLibrary library, string name, DateTimeOffset now)
    {
        if (plan.Assignments.Count == 0)
            throw new InvalidOperationException("Assign at least one cursor to a pointer before importing.");
        return library.Create(name, SetKind.Imported, plan.Assignments, now, author: plan.Author);
    }

    private ImportPlan PlanZip(string zipPath)
    {
        var temp = Path.Combine(tempRoot, "import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temp);
        try
        {
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                if (zip.Entries.Count > MaxZipEntries)
                    throw new InvalidOperationException($"The .zip holds more than {MaxZipEntries} files, which is too many for a cursor set.");
                if (zip.Entries.Sum(e => e.Length) > MaxZipBytes)
                    throw new InvalidOperationException("The .zip is larger than 200 MB once unpacked, which is too large for a cursor set.");

                var root = Path.GetFullPath(temp) + Path.DirectorySeparatorChar;
                foreach (var entry in zip.Entries)
                {
                    var ext = Path.GetExtension(entry.Name).ToLowerInvariant();
                    if (entry.Name.Length == 0 || ext is not (".cur" or ".ani" or ".inf"))
                        continue;
                    var dest = Path.GetFullPath(Path.Combine(temp, entry.FullName.Replace('\\', '/')));
                    if (!dest.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        continue; // entry tries to leave the folder; skip it
                    Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                    entry.ExtractToFile(dest);
                }
            }

            // Packs often wrap everything in one top folder; plan from the folder holding the .inf or most cursors.
            var folder = PickContentFolder(temp);
            var plan = PlanFolder(folder, Path.GetFileNameWithoutExtension(zipPath), temp);
            return plan;
        }
        catch
        {
            try { Directory.Delete(temp, recursive: true); } catch (IOException) { }
            throw;
        }
    }

    private static string PickContentFolder(string root)
    {
        var inf = Directory.EnumerateFiles(root, "*.inf", SearchOption.AllDirectories).FirstOrDefault();
        if (inf is not null)
            return Path.GetDirectoryName(inf)!;
        return Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
            .Where(IsCursorFile)
            .GroupBy(f => Path.GetDirectoryName(f)!)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .FirstOrDefault() ?? root;
    }

    private ImportPlan PlanFolder(string folder, string fallbackName, string? tempDirectory, string? infPath = null)
    {
        var warnings = new List<string>();
        infPath ??= Directory.EnumerateFiles(folder, "*.inf").FirstOrDefault();
        var cursorFiles = Directory.EnumerateFiles(folder).Where(IsCursorFile).Order(StringComparer.OrdinalIgnoreCase).ToList();
        if (cursorFiles.Count == 0)
            throw new InvalidOperationException("No .cur or .ani files were found to import.");

        var readable = cursorFiles.Where(f => IsReadable(f, warnings)).ToList();
        var assignments = new Dictionary<CursorRole, string>();
        string? name = null;

        if (infPath is not null)
        {
            var inf = InfParser.Parse(File.ReadAllText(infPath));
            name = inf.Name;
            foreach (var (role, fileName) in inf.Files)
            {
                var match = readable.FirstOrDefault(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                    assignments[role] = match;
                else
                    warnings.Add($"The install file lists {fileName} for {CursorRoles.Info(role).DisplayName}, but that file is missing or unreadable.");
            }
        }

        var leftovers = readable.Where(f => !assignments.ContainsValue(f)).ToList();
        var (guessed, unassigned) = AssignByName(leftovers);
        foreach (var (role, file) in guessed)
        {
            if (!assignments.TryAdd(role, file))
                unassigned.Add(file);
        }

        return new ImportPlan(name ?? fallbackName, assignments, unassigned, warnings, tempDirectory, null);
    }

    private static (Dictionary<CursorRole, string> Assigned, List<string> Unassigned) AssignByName(IEnumerable<string> files)
    {
        var assigned = new Dictionary<CursorRole, string>();
        var unassigned = new List<string>();
        foreach (var file in files)
        {
            var role = RoleGuesser.Guess(file);
            if (role is { } r && assigned.TryAdd(r, file))
                continue;
            unassigned.Add(file);
        }
        return (assigned, unassigned);
    }

    private static bool IsReadable(string file, List<string> warnings)
    {
        try
        {
            CursorReader.ReadFile(file);
            return true;
        }
        catch (CursorFormatException ex)
        {
            warnings.Add($"{Path.GetFileName(file)} was skipped: {ex.Message}");
        }
        catch (IOException ex)
        {
            warnings.Add($"{Path.GetFileName(file)} was skipped because it could not be opened ({ex.Message}).");
        }
        return false;
    }
}
