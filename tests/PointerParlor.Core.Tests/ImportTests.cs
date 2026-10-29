using System.IO.Compression;
using PointerParlor.Core.Import;
using PointerParlor.Core.Library;

namespace PointerParlor.Core.Tests;

public class RoleGuesserTests
{
    [Theory]
    [InlineData("Normal Select.cur", CursorRole.Arrow)]
    [InlineData("pointer.cur", CursorRole.Arrow)]
    [InlineData("arrow_r.cur", CursorRole.Arrow)]
    [InlineData("Help Select.cur", CursorRole.Help)]
    [InlineData("Working in Background.ani", CursorRole.AppStarting)]
    [InlineData("appstarting.ani", CursorRole.AppStarting)]
    [InlineData("busy.ani", CursorRole.Wait)]
    [InlineData("aero_busy.ani", CursorRole.Wait)]
    [InlineData("Precision Select.cur", CursorRole.Crosshair)]
    [InlineData("cross.cur", CursorRole.Crosshair)]
    [InlineData("Text Select.cur", CursorRole.IBeam)]
    [InlineData("beam_r.cur", CursorRole.IBeam)]
    [InlineData("Handwriting.cur", CursorRole.NWPen)]
    [InlineData("pen.cur", CursorRole.NWPen)]
    [InlineData("Unavailable.cur", CursorRole.No)]
    [InlineData("no.cur", CursorRole.No)]
    [InlineData("Vertical Resize.cur", CursorRole.SizeNS)]
    [InlineData("aero_ns.cur", CursorRole.SizeNS)]
    [InlineData("Horizontal Resize.cur", CursorRole.SizeWE)]
    [InlineData("horz.cur", CursorRole.SizeWE)]
    [InlineData("Diagonal Resize 1.cur", CursorRole.SizeNWSE)]
    [InlineData("dgn1.cur", CursorRole.SizeNWSE)]
    [InlineData("Diagonal Resize 2.cur", CursorRole.SizeNESW)]
    [InlineData("aero_nesw.cur", CursorRole.SizeNESW)]
    [InlineData("Move.cur", CursorRole.SizeAll)]
    [InlineData("Alternate Select.cur", CursorRole.UpArrow)]
    [InlineData("aero_up.cur", CursorRole.UpArrow)]
    [InlineData("Link Select.cur", CursorRole.Hand)]
    [InlineData("aero_link.cur", CursorRole.Hand)]
    [InlineData("Location Select.cur", CursorRole.Pin)]
    [InlineData("aero_pin.cur", CursorRole.Pin)]
    [InlineData("Person Select.cur", CursorRole.Person)]
    public void Guesses_common_names(string file, CursorRole role)
    {
        Assert.Equal(role, RoleGuesser.Guess(file));
    }

    [Theory]
    [InlineData("spinner.ani")]
    [InlineData("sparkle.cur")]
    public void Unknown_names_have_no_guess(string file)
    {
        Assert.Null(RoleGuesser.Guess(file));
    }
}

public class InfParserTests
{
    private const string TypicalInf = """
        ; Night Owl cursor pack
        [Version]
        signature="$CHICAGO$"

        [DefaultInstall]
        CopyFiles = Scheme.Cur
        AddReg    = Scheme.Reg

        [DestinationDirs]
        Scheme.Cur = 10,"%CUR_DIR%"

        [Scheme.Reg]
        HKCU,"Control Panel\Cursors\Schemes","%SCHEME_NAME%",,"%10%\%CUR_DIR%\%pointer%,%10%\%CUR_DIR%\%help%,%10%\%CUR_DIR%\%work%,%10%\%CUR_DIR%\%busy%,,%10%\%CUR_DIR%\%text%"

        [Scheme.Cur]
        "Normal Select.cur"

        [Strings]
        CUR_DIR       = "Cursors\Night Owl"
        SCHEME_NAME   = "Night Owl"
        pointer       = "Normal Select.cur"
        help          = "Help; Select.cur"
        work          = "Working.ani"
        busy          = "Busy.ani"
        text          = "Text.cur"
        """;

    [Fact]
    public void Reads_scheme_name_and_files_from_scheme_line()
    {
        var inf = InfParser.Parse(TypicalInf);

        Assert.Equal("Night Owl", inf.Name);
        Assert.Equal(5, inf.Files.Count);
        Assert.Equal("Normal Select.cur", inf.Files[CursorRole.Arrow]);
        Assert.Equal("Help; Select.cur", inf.Files[CursorRole.Help]);
        Assert.Equal("Busy.ani", inf.Files[CursorRole.Wait]);
        Assert.Equal("Text.cur", inf.Files[CursorRole.IBeam]);
        Assert.False(inf.Files.ContainsKey(CursorRole.Crosshair));
    }

    [Fact]
    public void Reads_per_role_lines_when_there_is_no_scheme_line()
    {
        var inf = InfParser.Parse("""
            [Install.Reg]
            HKCU,"Control Panel\Cursors",Arrow,0x00020000,"%10%\Cursors\Owl\arrow.cur"
            HKCU,"Control Panel\Cursors",Hand,0x00020000,"%10%\Cursors\Owl\link.cur"
            HKCU,"Control Panel\Cursors",Sparkle,0x00020000,"%10%\Cursors\Owl\x.cur"
            [Strings]
            SCHEME_NAME = "Owl"
            """);

        Assert.Equal("Owl", inf.Name);
        Assert.Equal(2, inf.Files.Count);
        Assert.Equal("link.cur", inf.Files[CursorRole.Hand]);
    }

    [Fact]
    public void Text_without_registry_lines_has_no_files()
    {
        var inf = InfParser.Parse("[Version]\nsignature=\"$CHICAGO$\"\n");
        Assert.Null(inf.Name);
        Assert.Empty(inf.Files);
    }
}

public class SetImporterTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Folder_with_inf_uses_its_name_and_mapping_then_guesses_the_rest()
    {
        using var tmp = new TempDir();
        tmp.Text("owl/install.inf", """
            [Scheme.Reg]
            HKCU,"Control Panel\Cursors\Schemes","%SCHEME_NAME%",,"%10%\%CUR_DIR%\%pointer%,,,%10%\%CUR_DIR%\%gone%"
            [Strings]
            CUR_DIR = "Cursors\Owl"
            SCHEME_NAME = "Owl Deluxe"
            pointer = "a1.cur"
            gone = "missing.ani"
            """);
        tmp.File("owl/a1.cur", Fixtures.Cur());
        tmp.File("owl/link.cur", Fixtures.Cur());
        tmp.File("owl/mystery.cur", Fixtures.Cur());
        tmp.File("owl/broken.cur", [1, 2, 3]);

        using var plan = new SetImporter(tmp.Path).Plan(Path.Combine(tmp.Path, "owl"));

        Assert.Equal("Owl Deluxe", plan.SuggestedName);
        Assert.EndsWith("a1.cur", plan.Assignments[CursorRole.Arrow]);
        Assert.EndsWith("link.cur", plan.Assignments[CursorRole.Hand]);
        Assert.Single(plan.Unassigned, f => f.EndsWith("mystery.cur"));
        Assert.Contains(plan.Warnings, w => w.Contains("broken.cur"));
        Assert.Contains(plan.Warnings, w => w.Contains("missing.ani"));
    }

    [Fact]
    public void Inf_file_path_imports_its_folder()
    {
        using var tmp = new TempDir();
        var inf = tmp.Text("p/setup.inf", "[Strings]\nSCHEME_NAME=\"Pack\"\n");
        tmp.File("p/busy.ani", Fixtures.Ani());

        using var plan = new SetImporter(tmp.Path).Plan(inf);

        Assert.Equal("Pack", plan.SuggestedName);
        Assert.True(plan.Assignments.ContainsKey(CursorRole.Wait));
    }

    [Fact]
    public void Folder_without_cursors_is_refused()
    {
        using var tmp = new TempDir();
        tmp.Text("empty/readme.txt", "hi");
        Assert.Throws<InvalidOperationException>(() => new SetImporter(tmp.Path).Plan(Path.Combine(tmp.Path, "empty")));
    }

    [Fact]
    public void Unsupported_file_type_is_refused()
    {
        using var tmp = new TempDir();
        var png = tmp.File("x.png", [1]);
        var ex = Assert.Throws<InvalidOperationException>(() => new SetImporter(tmp.Path).Plan(png));
        Assert.Contains("x.png", ex.Message);
    }

    [Fact]
    public void Single_file_with_unknown_name_becomes_the_normal_pointer()
    {
        using var tmp = new TempDir();
        var file = tmp.File("sparkle.cur", Fixtures.Cur());

        using var plan = new SetImporter(tmp.Path).Plan(file);

        Assert.Equal("sparkle", plan.SuggestedName);
        Assert.Equal(file, plan.Assignments[CursorRole.Arrow]);
        Assert.Empty(plan.Unassigned);
    }

    [Fact]
    public void Several_loose_files_form_one_set_named_after_their_folder()
    {
        using var tmp = new TempDir();
        var files = new[] { tmp.File("Pastel/busy.ani", Fixtures.Ani()), tmp.File("Pastel/help.cur", Fixtures.Cur()) };

        using var plan = new SetImporter(tmp.Path).PlanFiles(files);

        Assert.Equal("Pastel", plan.SuggestedName);
        Assert.Equal(2, plan.Assignments.Count);
    }

    [Fact]
    public void Zip_with_wrapper_folder_imports_and_cleans_up_on_dispose()
    {
        using var tmp = new TempDir();
        var zipPath = Path.Combine(tmp.Path, "Candy Pack.zip");
        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            Add(zip, "Candy/arrow.cur", Fixtures.Cur());
            Add(zip, "Candy/busy.ani", Fixtures.Ani());
            Add(zip, "Candy/readme.txt", [1, 2]);
            Add(zip, "../escape.cur", Fixtures.Cur());
        }
        var tempRoot = Path.Combine(tmp.Path, "temp");

        var plan = new SetImporter(tempRoot).Plan(zipPath);
        Assert.Equal("Candy Pack", plan.SuggestedName);
        Assert.Equal(2, plan.Assignments.Count);
        Assert.False(File.Exists(Path.Combine(tmp.Path, "escape.cur")));

        var set = new SetImporter(tempRoot).Commit(plan, new CursorLibrary(Path.Combine(tmp.Path, "lib")), "Candy", Now);
        plan.Dispose();

        Assert.Equal(SetKind.Imported, set.Kind);
        Assert.True(File.Exists(set.Paths[CursorRole.Wait]));
        Assert.Empty(Directory.EnumerateDirectories(tempRoot));
    }

    [Fact]
    public void Commit_needs_at_least_one_assignment()
    {
        using var tmp = new TempDir();
        tmp.File("f/mystery1.cur", Fixtures.Cur());
        tmp.File("f/mystery2.cur", Fixtures.Cur());
        var importer = new SetImporter(tmp.Path);
        using var plan = importer.Plan(Path.Combine(tmp.Path, "f"));

        Assert.Empty(plan.Assignments);
        Assert.Throws<InvalidOperationException>(() => importer.Commit(plan, new CursorLibrary(Path.Combine(tmp.Path, "lib")), "X", Now));
    }

    private static void Add(ZipArchive zip, string name, byte[] content)
    {
        using var s = zip.CreateEntry(name).Open();
        s.Write(content);
    }
}
