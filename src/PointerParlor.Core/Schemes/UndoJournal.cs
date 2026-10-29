using PointerParlor.Core.Library;

namespace PointerParlor.Core.Schemes;

/// <summary>Durable, single-level undo for registry changes and their associated library files.</summary>
public sealed class UndoJournal(string snapshotPath, string libraryRoot)
{
    private readonly string _root = Path.GetFullPath(libraryRoot);
    private readonly string _stagingRoot = Path.Combine(Path.GetFullPath(libraryRoot), ".undo");

    public LibraryUndo CaptureRename(CursorSet set)
    {
        var directory = OwnedSetDirectory(set);
        return new(directory, null, File.ReadAllText(Path.Combine(directory, CursorLibrary.ManifestFileName)));
    }

    public LibraryUndo CaptureDelete(CursorSet set) =>
        new(OwnedSetDirectory(set), Path.Combine(_stagingRoot, Guid.NewGuid().ToString("N")), null);

    public void Execute(ApplySnapshot snapshot, ApplySnapshot? previous, Action<ApplySnapshot> publishSnapshot, Action mutate)
    {
        Validate(snapshot.LibraryUndo);
        Validate(previous?.LibraryUndo);
        Save(snapshot);
        // Publish before mutation: even a partly failed registry apply remains undoable.
        publishSnapshot(snapshot);
        Discard(previous?.LibraryUndo);
        try
        {
            mutate();
        }
        catch
        {
            RestoreLibrary(snapshot.LibraryUndo);
            throw;
        }
    }

    public void StageDelete(LibraryUndo undo)
    {
        Validate(undo);
        if (undo.StagedDirectory is null)
            throw new InvalidOperationException("Deletion has no staging directory.");
        Directory.CreateDirectory(_stagingRoot);
        RejectLinks(_stagingRoot);
        Directory.Move(undo.Directory, undo.StagedDirectory);
    }

    public void RestoreLibrary(LibraryUndo? undo)
    {
        Validate(undo);
        if (undo is null)
            return;
        if (undo.StagedDirectory is { } staged && Directory.Exists(staged))
        {
            if (Directory.Exists(undo.Directory))
                throw new IOException("The deleted set's original directory is occupied; its undo copy was retained.");
            Directory.Move(staged, undo.Directory);
        }
        if (!Directory.Exists(undo.Directory))
            throw new IOException("The set required for undo is missing.");
        if (undo.ManifestJson is { } manifest)
            WriteAtomic(Path.Combine(undo.Directory, CursorLibrary.ManifestFileName), manifest);
    }

    public void Clear() => File.Delete(snapshotPath);

    private void Save(ApplySnapshot snapshot) => WriteAtomic(snapshotPath, snapshot.ToJson());

    private static void WriteAtomic(string path, string contents)
    {
        var temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                using var writer = new StreamWriter(stream, leaveOpen: true);
                writer.Write(contents);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(temp, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temp))
                File.Delete(temp);
        }
    }

    private string OwnedSetDirectory(CursorSet set)
    {
        if (set.IsReadOnly || set.Directory is null)
            throw new InvalidOperationException("Only imported sets and saved mixes can be changed.");
        var directory = Path.GetFullPath(set.Directory);
        RequireChild(directory, _root);
        RejectLinks(directory);
        return directory;
    }

    private void Validate(LibraryUndo? undo)
    {
        for (var ancestor = new DirectoryInfo(_root); ancestor is not null; ancestor = ancestor.Parent)
            if (ancestor.Exists && (ancestor.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("The library path cannot traverse symbolic links or junctions.");
        RejectLinks(_root);
        RejectLinks(_stagingRoot);
        if (undo is null)
            return;
        RequireChild(undo.Directory, _root);
        if (string.Equals(Path.GetFullPath(undo.Directory), _stagingRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The undo store is not a cursor set.");
        RejectLinks(undo.Directory);
        if (undo.StagedDirectory is { } staged)
        {
            RequireChild(staged, _stagingRoot);
            RejectLinks(staged);
        }
    }

    private static void RequireChild(string path, string parent)
    {
        var full = Path.GetFullPath(path);
        if (!string.Equals(Path.GetDirectoryName(full), parent, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Undo paths must remain in the owned library directory.");
    }

    private static void RejectLinks(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
            return;
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new InvalidOperationException("Undo cannot traverse symbolic links or junctions.");
        if (Directory.Exists(path))
            foreach (var child in Directory.EnumerateFileSystemEntries(path))
                RejectLinks(child);
    }

    private void Discard(LibraryUndo? previous)
    {
        if (previous?.StagedDirectory is { } staged && Directory.Exists(staged))
            Directory.Delete(staged, recursive: true);
    }
}
