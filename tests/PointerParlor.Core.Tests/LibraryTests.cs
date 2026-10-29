using PointerParlor.Core.Library;

namespace PointerParlor.Core.Tests;

public class CursorLibraryTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_copies_files_named_by_role_and_loads_back()
    {
        using var tmp = new TempDir();
        var arrow = tmp.File("src/my arrow, v2.cur", Fixtures.Cur());
        var busy = tmp.File("src/busy.ANI", Fixtures.Ani());
        var lib = new CursorLibrary(Path.Combine(tmp.Path, "lib"));

        var set = lib.Create("Night Owl", SetKind.Imported, new Dictionary<CursorRole, string> { [CursorRole.Arrow] = arrow, [CursorRole.Wait] = busy }, Now, author: "Ann");

        Assert.Equal("night-owl", set.Id);
        Assert.Equal(Path.Combine(set.Directory!, "Arrow.cur"), set.Paths[CursorRole.Arrow]);
        Assert.Equal(Path.Combine(set.Directory!, "Wait.ani"), set.Paths[CursorRole.Wait]);
        Assert.True(File.Exists(set.Paths[CursorRole.Wait]));

        var (sets, problems) = lib.LoadAll();
        Assert.Empty(problems);
        var loaded = Assert.Single(sets);
        Assert.Equal("Night Owl", loaded.Name);
        Assert.Equal("Ann", loaded.Manifest.Author);
        Assert.Equal(Now, loaded.Manifest.CreatedUtc);
        Assert.Equal(set.Paths, loaded.Paths);
        Assert.False(loaded.IsReadOnly);
        Assert.False(loaded.IsComplete);
    }

    [Fact]
    public void Same_name_twice_gets_a_distinct_id()
    {
        using var tmp = new TempDir();
        var file = tmp.File("a.cur", Fixtures.Cur());
        var lib = new CursorLibrary(Path.Combine(tmp.Path, "lib"));
        var roles = new Dictionary<CursorRole, string> { [CursorRole.Arrow] = file };

        Assert.Equal("ink", lib.Create("Ink", SetKind.Mine, roles, Now).Id);
        Assert.Equal("ink-2", lib.Create("Ink", SetKind.Mine, roles, Now).Id);
    }

    [Fact]
    public void Rename_and_delete_work_for_user_sets()
    {
        using var tmp = new TempDir();
        var lib = new CursorLibrary(Path.Combine(tmp.Path, "lib"));
        var set = lib.Create("Old", SetKind.Mine, new Dictionary<CursorRole, string> { [CursorRole.Arrow] = tmp.File("a.cur", Fixtures.Cur()) }, Now);

        var renamed = lib.Rename(set, "  New  ");
        Assert.Equal("New", renamed.Name);
        Assert.Equal("New", Assert.Single(lib.LoadAll().Sets).Name);

        lib.Delete(renamed);
        Assert.Empty(lib.LoadAll().Sets);
    }

    [Fact]
    public void Bundled_and_windows_sets_are_read_only()
    {
        var windows = new CursorSet(new SetManifest { Id = "w", Name = "W", Kind = SetKind.Windows }, null);
        var lib = new CursorLibrary(Path.GetTempPath());
        Assert.True(windows.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => lib.Delete(windows));
        Assert.Throws<InvalidOperationException>(() => lib.Rename(windows, "x"));
    }

    [Fact]
    public void Only_imported_and_mine_sets_can_be_created()
    {
        using var tmp = new TempDir();
        var lib = new CursorLibrary(tmp.Path);
        var roles = new Dictionary<CursorRole, string> { [CursorRole.Arrow] = tmp.File("a.cur", Fixtures.Cur()) };
        Assert.Throws<ArgumentException>(() => lib.Create("S", SetKind.Signature, roles, Now));
        Assert.Throws<ArgumentException>(() => lib.Create(" ", SetKind.Mine, roles, Now));
        Assert.Throws<ArgumentException>(() => lib.Create("S", SetKind.Mine, new Dictionary<CursorRole, string>(), Now));
    }

    [Fact]
    public void Failed_copy_leaves_no_half_made_set()
    {
        using var tmp = new TempDir();
        var lib = new CursorLibrary(Path.Combine(tmp.Path, "lib"));
        var roles = new Dictionary<CursorRole, string>
        {
            [CursorRole.Arrow] = tmp.File("a.cur", Fixtures.Cur()),
            [CursorRole.Hand] = Path.Combine(tmp.Path, "missing.cur"),
        };
        Assert.ThrowsAny<IOException>(() => lib.Create("Broken", SetKind.Imported, roles, Now));
        Assert.Empty(Directory.EnumerateDirectories(lib.RootDirectory));
    }

    [Fact]
    public void Broken_manifests_are_reported_not_fatal()
    {
        using var tmp = new TempDir();
        tmp.Text("lib/bad/set.json", "{ nope");
        tmp.Text("lib/noname/set.json", "{\"id\":\"noname\"}");
        tmp.Text("lib/stray-folder/readme.txt", "hi");

        var (sets, problems) = new CursorLibrary(Path.Combine(tmp.Path, "lib")).LoadAll();

        Assert.Empty(sets);
        Assert.Equal(2, problems.Count);
    }

    [Fact]
    public void Missing_library_folder_loads_as_empty()
    {
        var (sets, problems) = new CursorLibrary(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString())).LoadAll();
        Assert.Empty(sets);
        Assert.Empty(problems);
    }

    [Fact]
    public void InstallBundled_copies_new_sets_and_refreshes_only_newer_versions()
    {
        using var tmp = new TempDir();
        var bundled = Path.Combine(tmp.Path, "bundled");
        tmp.Text("bundled/lagoon/set.json", "{\"id\":\"lagoon\",\"name\":\"Lagoon\",\"kind\":\"Signature\",\"version\":1,\"cursors\":{\"Arrow\":\"Arrow.cur\"}}");
        tmp.File("bundled/lagoon/Arrow.cur", Fixtures.Cur(r: 1));
        tmp.Text("bundled/sneaky/set.json", "{\"id\":\"sneaky\",\"name\":\"S\",\"kind\":\"Imported\",\"cursors\":{}}");
        var lib = new CursorLibrary(Path.Combine(tmp.Path, "lib"));

        Assert.Equal(["lagoon"], lib.InstallBundled(bundled));
        Assert.Empty(lib.InstallBundled(bundled)); // same version: left alone

        tmp.Text("bundled/lagoon/set.json", "{\"id\":\"lagoon\",\"name\":\"Lagoon\",\"kind\":\"Signature\",\"version\":2,\"cursors\":{\"Arrow\":\"Arrow.cur\"}}");
        tmp.File("bundled/lagoon/Arrow.cur", Fixtures.Cur(r: 2));
        Assert.Equal(["lagoon"], lib.InstallBundled(bundled));

        var set = Assert.Single(lib.LoadAll().Sets);
        Assert.Equal(SetKind.Signature, set.Kind);
        Assert.Equal(2, set.Manifest.Version);
        Assert.True(set.IsReadOnly);
    }

    [Theory]
    [InlineData("Night Owl", "night-owl")]
    [InlineData("  Red, Blue & Green!! ", "red-blue-green")]
    [InlineData("✨✨", "set")]
    public void Slug_is_folder_safe_and_comma_free(string name, string slug)
    {
        Assert.Equal(slug, CursorLibrary.Slug(name));
    }
}

public class WindowsSchemesTests
{
    [Fact]
    public void Builds_read_only_sets_with_expanded_paths_default_first()
    {
        var schemes = new Dictionary<string, string>
        {
            ["Windows Black"] = @"%SystemRoot%\cursors\arrow_r.cur,%SystemRoot%\cursors\help_r.cur",
            ["Windows Default"] = @"%SystemRoot%\cursors\aero_arrow.cur",
            ["Empty"] = ",,,",
        };

        var sets = WindowsSchemes.FromRegistry(schemes, s => s.Replace("%SystemRoot%", @"C:\Windows"));

        Assert.Equal(["Windows Default", "Windows Black"], sets.Select(s => s.Name));
        Assert.Equal("windows-windows-default", sets[0].Id);
        Assert.Equal(@"C:\Windows\cursors\help_r.cur", sets[1].Paths[CursorRole.Help]);
        Assert.All(sets, s => Assert.Equal(SetKind.Windows, s.Kind));
        Assert.All(sets, s => Assert.Null(s.Directory));
    }
}

public class AppSettingsTests
{
    [Fact]
    public void Defaults_are_not_written_to_the_file()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "settings.json");
        var settings = AppSettings.Load(path);
        Assert.Equal(ThemeChoice.System, settings.Theme);
        settings.Save();
        Assert.DoesNotContain("theme", File.ReadAllText(path));
        Assert.Contains("\"schemaVersion\": 1", File.ReadAllText(path));
    }

    [Fact]
    public void Saved_values_load_back()
    {
        using var tmp = new TempDir();
        var path = Path.Combine(tmp.Path, "sub", "settings.json");
        var settings = AppSettings.Load(path);
        settings.Theme = ThemeChoice.Dark;
        settings.Save();
        Assert.Equal(ThemeChoice.Dark, AppSettings.Load(path).Theme);
    }

    [Fact]
    public void Corrupt_file_falls_back_to_defaults()
    {
        using var tmp = new TempDir();
        var path = tmp.Text("settings.json", "{{{");
        Assert.Equal(ThemeChoice.System, AppSettings.Load(path).Theme);
    }
}
