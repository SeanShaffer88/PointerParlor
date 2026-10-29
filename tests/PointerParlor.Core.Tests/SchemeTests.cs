using PointerParlor.Core.Library;
using PointerParlor.Core.Schemes;

namespace PointerParlor.Core.Tests;

public class CursorRoleTests
{
    [Fact]
    public void Has_17_roles_in_scheme_order()
    {
        Assert.Equal(17, CursorRoles.All.Count);
        Assert.Equal(
            "Arrow,Help,AppStarting,Wait,Crosshair,IBeam,NWPen,No,SizeNS,SizeWE,SizeNWSE,SizeNESW,SizeAll,UpArrow,Hand,Pin,Person",
            string.Join(',', CursorRoles.All.Select(r => r.RegistryName)));
        Assert.All(CursorRoles.All, r => Assert.Equal(r.Role, CursorRoles.All[(int)r.Role].Role));
    }

    [Fact]
    public void Parses_registry_names_case_insensitively()
    {
        Assert.True(CursorRoles.TryParseRegistryName("sizenwse", out var role));
        Assert.Equal(CursorRole.SizeNWSE, role);
        Assert.False(CursorRoles.TryParseRegistryName("Sparkle", out _));
    }
}

public class SchemeStringTests
{
    [Fact]
    public void Formats_17_entries_with_blanks_for_missing_roles()
    {
        var s = SchemeString.Format(new Dictionary<CursorRole, string> { [CursorRole.Arrow] = @"C:\a.cur", [CursorRole.Person] = @"C:\p.cur" });
        var parts = s.Split(',');
        Assert.Equal(17, parts.Length);
        Assert.Equal(@"C:\a.cur", parts[0]);
        Assert.Equal(@"C:\p.cur", parts[16]);
        Assert.Equal("", parts[5]);
    }

    [Fact]
    public void Parses_short_older_schemes_and_skips_blanks()
    {
        var parsed = SchemeString.Parse(@"%SystemRoot%\cursors\a.cur,,%SystemRoot%\cursors\w.ani");
        Assert.Equal(2, parsed.Count);
        Assert.Equal(@"%SystemRoot%\cursors\w.ani", parsed[CursorRole.AppStarting]);
    }

    [Fact]
    public void Refuses_paths_with_commas()
    {
        Assert.Throws<ArgumentException>(() => SchemeString.Format(new Dictionary<CursorRole, string> { [CursorRole.Arrow] = @"C:\a,b.cur" }));
    }
}

public class SchemePlannerTests
{
    private static readonly Dictionary<CursorRole, string> TwoRoles = new()
    {
        [CursorRole.Arrow] = @"C:\lib\lagoon\Arrow.cur",
        [CursorRole.Wait] = @"C:\lib\lagoon\Wait.ani",
    };

    [Fact]
    public void ApplySet_writes_every_role_and_registers_a_user_scheme()
    {
        var reg = new FakeRegistry();
        reg.Apply(SchemePlanner.ApplySet("Lagoon", TwoRoles));

        Assert.Equal(RegValue.Expand(@"C:\lib\lagoon\Arrow.cur"), reg.Read(RegistryPaths.Cursors, "Arrow"));
        Assert.Equal(RegValue.Expand(""), reg.Read(RegistryPaths.Cursors, "Hand")); // missing role cleared to default
        Assert.Equal(RegValue.Str("PointerParlor - Lagoon"), reg.Read(RegistryPaths.Cursors, ""));
        Assert.Equal(RegValue.DWord(1), reg.Read(RegistryPaths.Cursors, "Scheme Source"));
        var scheme = reg.Read(RegistryPaths.UserSchemes, "PointerParlor - Lagoon");
        Assert.Equal(RegValueType.ExpandString, scheme!.Type);
        Assert.Equal(TwoRoles, SchemeString.Parse(scheme.Text!));
        Assert.Equal(17 + 3, reg.Count);
    }

    [Fact]
    public void Scheme_names_never_contain_commas()
    {
        Assert.Equal("PointerParlor - Red  Blue", SchemePlanner.SchemeNameFor("Red, Blue"));
    }

    [Fact]
    public void ApplySingle_changes_one_role_and_clears_the_scheme_name()
    {
        var changes = SchemePlanner.ApplySingle(CursorRole.Hand, @"C:\x\Hand.cur");
        Assert.Equal(2, changes.Count);
        Assert.Contains(new RegistryChange(RegistryPaths.Cursors, "Hand", RegValue.Expand(@"C:\x\Hand.cur")), changes);
        Assert.Contains(new RegistryChange(RegistryPaths.Cursors, "", RegValue.Str("")), changes);
    }

    [Fact]
    public void ApplySystemScheme_marks_source_as_system_and_registers_nothing()
    {
        var changes = SchemePlanner.ApplySystemScheme("Windows Black", TwoRoles);
        Assert.DoesNotContain(changes, c => c.KeyPath == RegistryPaths.UserSchemes);
        Assert.Contains(new RegistryChange(RegistryPaths.Cursors, "Scheme Source", RegValue.DWord(2)), changes);
    }

    [Fact]
    public void Reset_uses_windows_default_scheme_when_known_else_clears_everything()
    {
        var withDefault = SchemePlanner.ResetToDefault(TwoRoles);
        Assert.Contains(new RegistryChange(RegistryPaths.Cursors, "", RegValue.Str("Windows Default")), withDefault);

        var reg = new FakeRegistry();
        reg.Apply(SchemePlanner.ResetToDefault(null));
        Assert.All(CursorRoles.All, r => Assert.Equal(RegValue.Expand(""), reg.Read(RegistryPaths.Cursors, r.RegistryName)));
        Assert.Equal(RegValue.DWord(0), reg.Read(RegistryPaths.Cursors, "Scheme Source"));
    }

    [Theory]
    [InlineData(31)]
    [InlineData(257)]
    public void Pointer_size_must_be_in_range(int size)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => SchemePlanner.SetPointerSize(size));
    }

    [Fact]
    public void Pointer_size_writes_cursor_base_size()
    {
        var change = Assert.Single(SchemePlanner.SetPointerSize(64));
        Assert.Equal(new RegistryChange(RegistryPaths.Cursors, "CursorBaseSize", RegValue.DWord(64)), change);
    }

    [Fact]
    public void RemoveScheme_deletes_the_registered_value()
    {
        var change = Assert.Single(SchemePlanner.RemoveScheme("Lagoon"));
        Assert.Null(change.Value);
        Assert.Equal("PointerParlor - Lagoon", change.ValueName);
    }
}

public class ApplySnapshotTests
{
    [Fact]
    public void Undo_restores_previous_values_and_deletes_new_ones()
    {
        var reg = new FakeRegistry();
        reg.Set(RegistryPaths.Cursors, "Arrow", RegValue.Expand(@"%SystemRoot%\cursors\aero_arrow.cur"));
        reg.Set(RegistryPaths.Cursors, "", RegValue.Str("Windows Default"));
        var changes = SchemePlanner.ApplySet("Lagoon", new Dictionary<CursorRole, string> { [CursorRole.Arrow] = @"C:\l\Arrow.cur" });

        var snapshot = ApplySnapshot.Capture("Apply Lagoon", changes, reg.Read, DateTimeOffset.UnixEpoch);
        reg.Apply(changes);
        reg.Apply(snapshot.UndoChanges());

        Assert.Equal(RegValue.Expand(@"%SystemRoot%\cursors\aero_arrow.cur"), reg.Read(RegistryPaths.Cursors, "Arrow"));
        Assert.Equal(RegValue.Str("Windows Default"), reg.Read(RegistryPaths.Cursors, ""));
        Assert.Null(reg.Read(RegistryPaths.UserSchemes, "PointerParlor - Lagoon"));
        Assert.Null(reg.Read(RegistryPaths.Cursors, "Hand"));
        Assert.Equal(2, reg.Count);
    }

    [Fact]
    public void Captures_each_value_once_even_if_changed_twice()
    {
        var changes = new[]
        {
            new RegistryChange(RegistryPaths.Cursors, "Arrow", RegValue.Expand("a")),
            new RegistryChange(RegistryPaths.Cursors, "ARROW", RegValue.Expand("b")),
        };
        var snapshot = ApplySnapshot.Capture("x", changes, new FakeRegistry().Read, DateTimeOffset.UnixEpoch);
        Assert.Single(snapshot.Entries);
    }

    [Fact]
    public void Survives_a_json_round_trip()
    {
        var reg = new FakeRegistry();
        reg.Set(RegistryPaths.Cursors, "CursorBaseSize", RegValue.DWord(48));
        var snapshot = ApplySnapshot.Capture("Size", SchemePlanner.SetPointerSize(64).Concat(SchemePlanner.ApplySingle(CursorRole.Pin, "p")), reg.Read,
            new DateTimeOffset(2026, 10, 29, 0, 0, 0, TimeSpan.Zero));

        var back = ApplySnapshot.FromJson(snapshot.ToJson());

        Assert.NotNull(back);
        Assert.Equal(snapshot.Description, back.Description);
        Assert.Equal(snapshot.CreatedUtc, back.CreatedUtc);
        Assert.Equal(snapshot.Entries, back.Entries);
    }

    [Fact]
    public void Broken_json_reads_as_no_snapshot()
    {
        Assert.Null(ApplySnapshot.FromJson("{not json"));
    }
}

public class ActiveStateTests
{
    private static string Expand(string s) => s.Replace("%SystemRoot%", @"C:\Windows", StringComparison.OrdinalIgnoreCase);

    private static CursorSet Set(string id, Dictionary<CursorRole, string> paths, SetKind kind = SetKind.Imported) =>
        new(new SetManifest
        {
            Id = id,
            Name = id,
            Kind = kind,
            Cursors = paths.ToDictionary(p => CursorRoles.Info(p.Key).RegistryName, p => p.Value, StringComparer.OrdinalIgnoreCase),
        }, null);

    [Fact]
    public void All_empty_is_windows_default()
    {
        var state = ActiveState.Detect(new Dictionary<CursorRole, string>(), [], Expand);
        Assert.Equal(ActiveKind.WindowsDefault, state.Kind);
        Assert.All(state.Cursors, c => Assert.Null(c.Path));
    }

    [Fact]
    public void Matches_a_set_ignoring_case_slashes_and_variables()
    {
        var windows = Set("windows-default", new() { [CursorRole.Arrow] = @"C:\Windows\cursors\aero_arrow.cur" }, SetKind.Windows);
        var current = new Dictionary<CursorRole, string> { [CursorRole.Arrow] = "%SYSTEMROOT%/Cursors/AERO_ARROW.CUR" };

        var state = ActiveState.Detect(current, [windows], Expand);

        Assert.Equal(ActiveKind.Set, state.Kind);
        Assert.Same(windows, state.Set);
    }

    [Fact]
    public void Mixed_pointers_report_where_each_came_from()
    {
        var lagoon = Set("lagoon", new() { [CursorRole.Arrow] = @"C:\l\Arrow.cur", [CursorRole.Hand] = @"C:\l\Hand.cur" });
        var ink = Set("ink", new() { [CursorRole.Arrow] = @"C:\i\Arrow.cur", [CursorRole.Hand] = @"C:\i\Hand.cur" });
        var current = new Dictionary<CursorRole, string>
        {
            [CursorRole.Arrow] = @"C:\l\Arrow.cur",
            [CursorRole.Hand] = @"C:\i\Hand.cur",
            [CursorRole.Pin] = @"D:\loose\pin.cur",
        };

        var state = ActiveState.Detect(current, [lagoon, ink], Expand);

        Assert.Equal(ActiveKind.Mix, state.Kind);
        Assert.Same(lagoon, state.Cursors[(int)CursorRole.Arrow].FromSet);
        Assert.Same(ink, state.Cursors[(int)CursorRole.Hand].FromSet);
        Assert.Null(state.Cursors[(int)CursorRole.Pin].FromSet);
        Assert.Equal(3, state.MixPaths.Count);
        Assert.Equal(@"D:\loose\pin.cur", state.MixPaths[CursorRole.Pin]);
    }

    [Fact]
    public void Partial_set_matches_only_when_its_missing_roles_are_default()
    {
        var partial = Set("partial", new() { [CursorRole.Arrow] = @"C:\p\Arrow.cur" });
        var exact = new Dictionary<CursorRole, string> { [CursorRole.Arrow] = @"C:\p\Arrow.cur" };
        var extra = new Dictionary<CursorRole, string> { [CursorRole.Arrow] = @"C:\p\Arrow.cur", [CursorRole.Hand] = @"C:\x\Hand.cur" };

        Assert.Equal(ActiveKind.Set, ActiveState.Detect(exact, [partial], Expand).Kind);
        Assert.Equal(ActiveKind.Mix, ActiveState.Detect(extra, [partial], Expand).Kind);
    }
}
