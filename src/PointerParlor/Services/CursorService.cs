using PointerParlor.Core;
using PointerParlor.Core.Import;
using PointerParlor.Core.Library;
using PointerParlor.Core.Schemes;

namespace PointerParlor.Services;

/// <summary>
/// Everything the pages do with cursors: the gallery's sets, what is applied now, applying,
/// undo, import and saving mixes. Every registry change is snapshotted first so it can be undone.
/// </summary>
public sealed class CursorService
{
    private readonly AppPaths _paths;
    private readonly CursorLibrary _library;
    private readonly SetImporter _importer;

    public CursorService(AppPaths paths)
    {
        _paths = paths;
        _library = new CursorLibrary(paths.Library);
        _importer = new SetImporter(paths.Temp);
        Directory.CreateDirectory(paths.Library);
        LastSnapshot = File.Exists(paths.UndoSnapshot) ? ApplySnapshot.FromJson(File.ReadAllText(paths.UndoSnapshot)) : null;
    }

    public event EventHandler? Changed;

    public IReadOnlyList<CursorSet> Sets { get; private set; } = [];
    public IReadOnlyList<string> LibraryProblems { get; private set; } = [];
    public ActiveCursorState Active { get; private set; } = new(ActiveKind.WindowsDefault, null, []);
    public ApplySnapshot? LastSnapshot { get; private set; }
    public int PointerSize { get; private set; } = SchemePlanner.MinPointerSize;

    public static string Expand(string path) => Environment.ExpandEnvironmentVariables(path);

    /// <summary>Copies bundled sets into the library (first run and after updates), then loads everything.</summary>
    public void Initialize()
    {
        _library.InstallBundled(Path.Combine(AppContext.BaseDirectory, "Sets"));
        Reload();
    }

    public void Reload()
    {
        var (librarySets, problems) = _library.LoadAll();
        var windowsSets = WindowsSchemes.FromRegistry(RegistryApplier.ReadSystemSchemes(), Expand);
        Sets = librarySets.Concat(windowsSets).ToList();
        LibraryProblems = problems;
        RefreshActive(force: true);
    }

    /// <summary>Re-reads what Windows is using. Raises <see cref="Changed"/> only when something differs, unless forced.</summary>
    public void RefreshActive(bool force = false)
    {
        var active = ActiveState.Detect(RegistryApplier.ReadCurrentCursors(), Sets, Expand);
        var size = RegistryApplier.ReadPointerSize();
        var same = Signature(active) == Signature(Active) && size == PointerSize;
        Active = active;
        PointerSize = size;
        if (force || !same)
            Changed?.Invoke(this, EventArgs.Empty);
    }

    private static string Signature(ActiveCursorState state) =>
        $"{state.Kind}|{state.Set?.Id}|" + string.Join('|', state.Cursors.Select(c => c.Path));

    public CursorSet? Find(string id) => Sets.FirstOrDefault(s => s.Id == id);

    public void ApplySet(CursorSet set)
    {
        var changes = set.Kind == SetKind.Windows
            ? SchemePlanner.ApplySystemScheme(set.Name, set.Paths)
            : SchemePlanner.ApplySet(set.Name, set.Paths);
        Run($"Apply {set.Name}", changes);
    }

    public void ApplySingle(CursorSet set, CursorRole role)
    {
        if (!set.Paths.TryGetValue(role, out var path))
            throw new InvalidOperationException($"{set.Name} has no {CursorRoles.Info(role).DisplayName} pointer.");
        Run($"Apply {CursorRoles.Info(role).DisplayName} from {set.Name}", SchemePlanner.ApplySingle(role, path));
    }

    public void ResetToDefault()
    {
        var windowsDefault = Sets.FirstOrDefault(s => s.Kind == SetKind.Windows && s.Name == WindowsSchemes.DefaultSchemeName);
        Run("Reset to Windows default", SchemePlanner.ResetToDefault(windowsDefault?.Paths));
    }

    public void SetPointerSize(int pixels)
    {
        if (pixels == PointerSize)
            return;
        Run($"Pointer size {pixels} px", SchemePlanner.SetPointerSize(pixels));
    }

    public bool CanUndo => LastSnapshot is not null;

    public void Undo()
    {
        if (LastSnapshot is null)
            return;
        RegistryApplier.Apply(LastSnapshot.UndoChanges());
        RegistryApplier.ReloadCursors();
        LastSnapshot = null;
        File.Delete(_paths.UndoSnapshot);
        RefreshActive(force: true);
    }

    /// <summary>Saves the current mix as a set under My sets, copying each pointer into the library.</summary>
    public CursorSet SaveMix(string name)
    {
        var paths = Active.MixPaths;
        if (paths.Count == 0)
            throw new InvalidOperationException("There are no custom pointers to save. Every pointer is the Windows default.");
        var set = _library.Create(name, SetKind.Mine, paths, DateTimeOffset.UtcNow);
        Reload();
        // The new set holds copies, so point Windows at them; then the mix reads as this set.
        ApplySet(set);
        return set;
    }

    public CursorSet Rename(CursorSet set, string newName)
    {
        var renamed = _library.Rename(set, newName);
        var oldScheme = SchemePlanner.SchemeNameFor(set.Name);
        if (RegistryApplier.Read(RegistryPaths.UserSchemes, oldScheme) is not null)
        {
            RegistryApplier.Apply(SchemePlanner.RemoveScheme(set.Name));
            RegistryApplier.Apply([new RegistryChange(RegistryPaths.UserSchemes, SchemePlanner.SchemeNameFor(renamed.Name), RegValue.Expand(SchemeString.Format(renamed.Paths)))]);
            if (RegistryApplier.Read(RegistryPaths.Cursors, "")?.Text == oldScheme)
                RegistryApplier.Apply([new RegistryChange(RegistryPaths.Cursors, "", RegValue.Str(SchemePlanner.SchemeNameFor(renamed.Name)))]);
        }
        Reload();
        return renamed;
    }

    /// <summary>Deletes a user set. Refused while any of its pointers is in use, since Windows reads the files directly.</summary>
    public void Delete(CursorSet set)
    {
        if (IsInUse(set))
            throw new InvalidOperationException($"{set.Name} is in use. Apply a different set or reset to the Windows default first, then delete it.");
        _library.Delete(set);
        RegistryApplier.Apply(SchemePlanner.RemoveScheme(set.Name));
        Reload();
    }

    public bool IsInUse(CursorSet set) =>
        Active.Set?.Id == set.Id || Active.Cursors.Any(c => c.FromSet?.Id == set.Id);

    public ImportPlan PlanImport(IReadOnlyList<string> paths) =>
        paths.Count == 1 ? _importer.Plan(paths[0]) : _importer.PlanFiles(paths.Where(SetImporter.IsCursorFile).ToList());

    public CursorSet CommitImport(ImportPlan plan, string name)
    {
        var set = _importer.Commit(plan, _library, name, DateTimeOffset.UtcNow);
        Reload();
        return set;
    }

    private void Run(string description, IReadOnlyList<RegistryChange> changes)
    {
        var snapshot = ApplySnapshot.Capture(description, changes, RegistryApplier.Read, DateTimeOffset.UtcNow);
        RegistryApplier.Apply(changes);
        RegistryApplier.ReloadCursors();
        LastSnapshot = snapshot;
        File.WriteAllText(_paths.UndoSnapshot, snapshot.ToJson());
        AppLog.Info($"{description} ({changes.Count} registry values)");
        RefreshActive(force: true);
    }
}
