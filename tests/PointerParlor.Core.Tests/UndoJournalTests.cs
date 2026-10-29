using PointerParlor.Core.Library;
using PointerParlor.Core.Schemes;
using System.Diagnostics;

namespace PointerParlor.Core.Tests;

public class UndoJournalTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.UnixEpoch;

    [Fact]
    public void Complete_snapshot_is_durable_and_published_before_mutation()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "undo.json");
        var journal = new UndoJournal(path, tmp.Path);
        var reg = new FakeRegistry();
        reg.Set(RegistryPaths.Cursors, "Arrow", RegValue.Expand("old.cur"));
        var changes = SchemePlanner.ApplySingle(CursorRole.Arrow, "new.cur");
        var snapshot = ApplySnapshot.Capture("Apply", changes, reg.Read, Now);
        ApplySnapshot? published = null;

        journal.Execute(snapshot, null, saved => published = saved, () =>
        {
            Assert.Same(snapshot, published);
            var saved = ApplySnapshot.FromJson(File.ReadAllText(path));
            Assert.NotNull(saved);
            Assert.Equal(snapshot.Entries, saved.Entries);
            reg.Apply(changes);
        });
        reg.Apply(ApplySnapshot.FromJson(File.ReadAllText(path))!.UndoChanges());
        Assert.Equal(RegValue.Expand("old.cur"), reg.Read(RegistryPaths.Cursors, "Arrow"));
    }

    [Fact]
    public void Journal_replace_failure_refuses_all_mutation_and_preserves_existing_state()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "undo.json");
        Directory.CreateDirectory(path); // Cannot atomically replace a directory with a file.
        var sentinel = Path.Combine(path, "keep.txt");
        File.WriteAllText(sentinel, "previous state");
        var journal = new UndoJournal(path, tmp.Path);
        var snapshot = new ApplySnapshot("Apply", Now, []);
        var published = false;
        var mutated = false;

        var error = Record.Exception(() => journal.Execute(snapshot, null, _ => published = true, () => mutated = true));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.False(published);
        Assert.False(mutated);
        Assert.Equal("previous state", File.ReadAllText(sentinel));
        Assert.Empty(Directory.GetFiles(tmp.Path, "*.tmp"));
    }

    [Fact]
    public void Delete_and_restart_undo_restore_cursor_files_before_registry_paths()
    {
        using var tmp = new TempDir();
        var (library, set) = CreateSet(tmp);
        var path = Path.Combine(tmp.Path, "undo.json");
        var journal = new UndoJournal(path, library.RootDirectory);
        var reg = new FakeRegistry();
        var schemeName = SchemePlanner.SchemeNameFor(set.Name);
        var scheme = RegValue.Expand(SchemeString.Format(set.Paths));
        reg.Set(RegistryPaths.UserSchemes, schemeName, scheme);
        var changes = SchemePlanner.RemoveScheme(set.Name);
        var metadata = journal.CaptureDelete(set);
        var snapshot = ApplySnapshot.Capture("Delete", changes, reg.Read, Now) with { LibraryUndo = metadata };

        journal.Execute(snapshot, null, _ => { }, () => { journal.StageDelete(metadata); reg.Apply(changes); });
        Assert.Empty(library.LoadAll().Sets);
        Assert.False(File.Exists(set.Paths[CursorRole.Arrow]));
        Assert.Null(reg.Read(RegistryPaths.UserSchemes, schemeName));

        var saved = ApplySnapshot.FromJson(File.ReadAllText(path))!;
        new UndoJournal(path, library.RootDirectory).RestoreLibrary(saved.LibraryUndo);
        Assert.True(File.Exists(set.Paths[CursorRole.Arrow]));
        reg.Apply(saved.UndoChanges());
        Assert.Equal(scheme, reg.Read(RegistryPaths.UserSchemes, schemeName));
        Assert.Equal(set.Name, Assert.Single(library.LoadAll().Sets).Name);
    }

    [Fact]
    public void Rename_undo_restores_manifest_and_both_scheme_names()
    {
        using var tmp = new TempDir();
        var (library, set) = CreateSet(tmp);
        var journal = new UndoJournal(Path.Combine(tmp.Path, "undo.json"), library.RootDirectory);
        var reg = new FakeRegistry();
        var oldName = SchemePlanner.SchemeNameFor(set.Name);
        var newName = SchemePlanner.SchemeNameFor("New");
        var value = RegValue.Expand(SchemeString.Format(set.Paths));
        reg.Set(RegistryPaths.UserSchemes, oldName, value);
        var changes = SchemePlanner.RemoveScheme(set.Name).Append(new RegistryChange(RegistryPaths.UserSchemes, newName, value)).ToList();
        var snapshot = ApplySnapshot.Capture("Rename", changes, reg.Read, Now) with { LibraryUndo = journal.CaptureRename(set) };

        journal.Execute(snapshot, null, _ => { }, () => { library.Rename(set, "New"); reg.Apply(changes); });
        Assert.Equal("New", Assert.Single(library.LoadAll().Sets).Name);
        journal.RestoreLibrary(snapshot.LibraryUndo);
        reg.Apply(snapshot.UndoChanges());
        Assert.Equal(set.Name, Assert.Single(library.LoadAll().Sets).Name);
        Assert.Equal(value, reg.Read(RegistryPaths.UserSchemes, oldName));
        Assert.Null(reg.Read(RegistryPaths.UserSchemes, newName));
    }

    [Fact]
    public void Mutation_failure_rolls_back_staged_files_and_retains_durable_registry_undo()
    {
        using var tmp = new TempDir();
        var (library, set) = CreateSet(tmp);
        var path = Path.Combine(tmp.Path, "undo.json");
        var journal = new UndoJournal(path, library.RootDirectory);
        var snapshot = new ApplySnapshot("Delete", Now, []) with { LibraryUndo = journal.CaptureDelete(set) };

        Assert.Throws<IOException>(() => journal.Execute(snapshot, null, _ => { }, () =>
        {
            journal.StageDelete(snapshot.LibraryUndo);
            throw new IOException("Simulated registry apply failure");
        }));
        Assert.True(File.Exists(set.Paths[CursorRole.Arrow]));
        Assert.NotNull(ApplySnapshot.FromJson(File.ReadAllText(path)));
        journal.RestoreLibrary(snapshot.LibraryUndo); // Recovery is repeatable after rollback.
    }

    [Fact]
    public void Untrusted_snapshot_cannot_restore_or_delete_outside_library()
    {
        using var tmp = new TempDir();
        var root = Path.Combine(tmp.Path, "library");
        Directory.CreateDirectory(root);
        var journal = new UndoJournal(Path.Combine(tmp.Path, "undo.json"), root);
        var external = new LibraryUndo(tmp.Path, Path.Combine(tmp.Path, "unowned"), null);
        var snapshot = new ApplySnapshot("Bad", Now, []) with { LibraryUndo = external };
        Assert.Throws<InvalidOperationException>(() => journal.RestoreLibrary(external));
        Assert.Throws<InvalidOperationException>(() => journal.Execute(snapshot, null, _ => { }, () => Assert.Fail("Must not mutate")));
    }

    [Fact]
    public void Replacing_undo_expires_only_the_previous_staged_set()
    {
        using var tmp = new TempDir();
        var (library, set) = CreateSet(tmp);
        var journal = new UndoJournal(Path.Combine(tmp.Path, "undo.json"), library.RootDirectory);
        var snapshot = new ApplySnapshot("Delete", Now, []) with { LibraryUndo = journal.CaptureDelete(set) };
        journal.Execute(snapshot, null, _ => { }, () => journal.StageDelete(snapshot.LibraryUndo));
        Assert.True(Directory.Exists(snapshot.LibraryUndo.StagedDirectory));
        journal.Execute(new ApplySnapshot("Next", Now, []), snapshot, _ => { }, () => { });
        Assert.False(Directory.Exists(snapshot.LibraryUndo.StagedDirectory));
    }

    [Fact]
    public void Legacy_registry_only_journals_remain_readable()
    {
        var snapshot = ApplySnapshot.FromJson("{\"Description\":\"Legacy\",\"CreatedUtc\":\"2026-10-29T00:00:00Z\",\"Entries\":[]}");
        Assert.NotNull(snapshot);
        Assert.Null(snapshot.LibraryUndo);
        Assert.Empty(snapshot.UndoChanges());
    }

    [Fact]
    public void Occupied_restore_directory_is_not_overwritten_and_staged_files_are_retained()
    {
        using var tmp = new TempDir();
        var (library, set) = CreateSet(tmp);
        var journal = new UndoJournal(Path.Combine(tmp.Path, "undo.json"), library.RootDirectory);
        var undo = journal.CaptureDelete(set);
        var snapshot = new ApplySnapshot("Delete", Now, []) with { LibraryUndo = undo };
        journal.Execute(snapshot, null, _ => { }, () => journal.StageDelete(undo));
        Directory.CreateDirectory(undo.Directory);
        var occupant = Path.Combine(undo.Directory, "keep.txt");
        File.WriteAllText(occupant, "new occupant");

        Assert.Throws<IOException>(() => journal.RestoreLibrary(undo));
        Assert.Equal("new occupant", File.ReadAllText(occupant));
        Assert.True(File.Exists(Path.Combine(undo.StagedDirectory!, "Arrow.cur")));
    }

    [Fact]
    public void Library_junctions_and_linked_ancestors_are_refused()
    {
        using var tmp = new TempDir();
        var root = Path.Combine(tmp.Path, "library");
        var target = Path.Combine(tmp.Path, "external");
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(target);
        var link = Path.Combine(root, "link");
        if (OperatingSystem.IsWindows())
        {
            // Directory junction creation is unprivileged, unlike Windows symbolic links.
            var info = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "/d", "/c", "mklink", "/J", link, target })
                info.ArgumentList.Add(arg);
            using var process = Process.Start(info)!;
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            Assert.True(process.WaitForExit(10000), "Junction creation timed out.");
            Assert.True(process.ExitCode == 0, output);
        }
        else
            Directory.CreateSymbolicLink(link, target);
        try
        {
            var journal = new UndoJournal(Path.Combine(tmp.Path, "undo.json"), root);
            Assert.Throws<InvalidOperationException>(() => journal.RestoreLibrary(null));
            Directory.CreateDirectory(Path.Combine(target, "nested"));
            var linkedRoot = new UndoJournal(Path.Combine(tmp.Path, "undo.json"), Path.Combine(link, "nested"));
            Assert.Throws<InvalidOperationException>(() => linkedRoot.RestoreLibrary(null));
        }
        finally
        {
            Directory.Delete(link);
        }
    }

    private static (CursorLibrary Library, CursorSet Set) CreateSet(TempDir tmp)
    {
        var library = new CursorLibrary(Path.Combine(tmp.Path, "library"));
        var set = library.Create("Old", SetKind.Mine,
            new Dictionary<CursorRole, string> { [CursorRole.Arrow] = tmp.File("source.cur", Fixtures.Cur()) }, Now);
        return (library, set);
    }
}
