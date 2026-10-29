using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PointerParlor.Core.Library;
using PointerParlor.ViewModels;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace PointerParlor.Views;

public sealed partial class GalleryPage : Page
{
    private static readonly SetKind[] GroupOrder = [SetKind.Signature, SetKind.OpenLicense, SetKind.Mine, SetKind.Imported, SetKind.Windows];

    public GalleryPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        App.Cursors.Changed += OnCursorsChanged;
        Rebuild();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        App.Cursors.Changed -= OnCursorsChanged;
    }

    private void OnCursorsChanged(object? sender, EventArgs e) => Rebuild();

    private void Rebuild()
    {
        var query = SearchBox.Text?.Trim() ?? "";
        var activeId = App.Cursors.Active.Set?.Id;
        var groups = App.Cursors.Sets
            .Where(s => query.Length == 0 || s.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
            .GroupBy(s => s.Kind)
            .OrderBy(g => Array.IndexOf(GroupOrder, g.Key))
            .Select(g => new SetGroup(SetGroup.TitleFor(g.Key), g.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase).Select(s => new SetCard(s, s.Id == activeId))))
            .ToList();
        GroupedSets.Source = groups;
        SetsGrid.ItemsSource = GroupedSets.View;
        EmptyState.Visibility = groups.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => Rebuild();

    private void OnSetClicked(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SetCard card)
            App.MainWindow.NavigateToSet(card.Set.Id);
    }

    private async void OnImportFiles(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        foreach (var ext in new[] { ".cur", ".ani", ".inf", ".zip" })
            picker.FileTypeFilter.Add(ext);
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        var files = await picker.PickMultipleFilesAsync();
        if (files.Count > 0)
            await ImportAsync(files.Select(f => f.Path).ToList());
    }

    private async void OnImportFolder(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { SuggestedStartLocation = PickerLocationId.Downloads };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
        var folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            await ImportAsync([folder.Path]);
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Import into PointerParlor";
        }
    }

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;
        var items = await e.DataView.GetStorageItemsAsync();
        var paths = items.Select(i => i.Path).Where(p => !string.IsNullOrEmpty(p)).ToList();
        if (paths.Count > 0)
            await ImportAsync(paths);
    }

    private async Task ImportAsync(IReadOnlyList<string> paths)
    {
        Busy.IsActive = true;
        Core.Import.ImportPlan? plan = null;
        try
        {
            plan = await Task.Run(() => App.Cursors.PlanImport(paths));
            Busy.IsActive = false;
            var name = await Dialogs.ReviewImportAsync(XamlRoot, plan);
            if (name is null)
                return;
            var set = App.Cursors.CommitImport(plan, name);
            App.MainWindow.NavigateToSet(set.Id);
        }
        catch (Exception ex)
        {
            App.MainWindow.ShowError("Couldn't import", ex);
        }
        finally
        {
            Busy.IsActive = false;
            plan?.Dispose();
        }
    }
}
