using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PointerParlor.Core;
using PointerParlor.Core.Library;
using PointerParlor.Services;
using PointerParlor.ViewModels;

namespace PointerParlor.Views;

public sealed partial class SetDetailPage : Page
{
    private string? _setId;
    private CursorSet? _set;
    private List<RoleTile> _tiles = [];

    public SetDetailPage()
    {
        InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _setId = e.Parameter as string;
        App.Cursors.Changed += OnCursorsChanged;
        Build();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        App.Cursors.Changed -= OnCursorsChanged;
    }

    private void OnCursorsChanged(object? sender, EventArgs e)
    {
        var current = _setId is null ? null : App.Cursors.Find(_setId);
        if (current is null || !ReferenceEquals(current, _set))
            Build();
        else
            UpdateApplied();
    }

    private void Build()
    {
        _set = _setId is null ? null : App.Cursors.Find(_setId);
        if (_set is null)
        {
            // The set was deleted or renamed away; go back to the gallery.
            if (Frame.CanGoBack)
                Frame.GoBack();
            return;
        }

        SetName.Text = _set.Name;
        var info = new List<string> { SetGroup.TitleFor(_set.Kind) };
        if (!string.IsNullOrWhiteSpace(_set.Manifest.Author)) info.Add("by " + _set.Manifest.Author);
        if (!string.IsNullOrWhiteSpace(_set.Manifest.License)) info.Add("License: " + _set.Manifest.License);
        info.Add($"{_set.Paths.Count} of {CursorRoles.All.Count} pointers");
        SetInfo.Text = string.Join(" · ", info);

        if (Uri.TryCreate(_set.Manifest.SourceUrl, UriKind.Absolute, out var source) && source.Scheme == Uri.UriSchemeHttps)
        {
            SourceLink.NavigateUri = source;
            SourceLink.Content = source.Host + source.AbsolutePath.TrimEnd('/');
            SourceLink.Visibility = Visibility.Visible;
        }
        else
        {
            SourceLink.Visibility = Visibility.Collapsed;
        }

        var editable = !_set.IsReadOnly;
        RenameButton.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;
        DeleteButton.Visibility = editable ? Visibility.Visible : Visibility.Collapsed;

        _tiles = CursorRoles.All
            .Select(r => new RoleTile(r, _set.Paths.GetValueOrDefault(r.Role), false, ApplyRole))
            .ToList();
        Tiles.ItemsSource = _tiles;
        UpdateApplied();
        _ = CheckSizesAsync(_set);
    }

    private void UpdateApplied()
    {
        if (_set is null)
            return;
        var active = App.Cursors.Active;
        var setInUse = active.Kind == ActiveKind.Set && active.Set?.Id == _set.Id;
        ApplySetButton.IsEnabled = !setInUse;
        ApplySetText.Text = setInUse ? "Set in use" : "Apply set";
        foreach (var tile in _tiles)
        {
            var current = active.Cursors.ElementAtOrDefault((int)tile.Info.Role)?.Path;
            tile.IsApplied = tile.Path is not null && current is not null
                && string.Equals(ActiveState.Normalize(current, CursorService.Expand), ActiveState.Normalize(tile.Path, CursorService.Expand), StringComparison.Ordinal);
        }
    }

    /// <summary>Warns when the set's images are smaller than the pointer size Windows will draw.</summary>
    private async Task CheckSizesAsync(CursorSet set)
    {
        SizeWarning.IsOpen = false;
        var sizes = await Task.WhenAll(set.Paths.Values.Select(PreviewLoader.LargestSizeAsync));
        var readable = sizes.Where(s => s > 0).ToList();
        if (_set != set || readable.Count == 0)
            return;
        var smallest = readable.Min();
        var pointerSize = App.Cursors.PointerSize;
        if (smallest < pointerSize)
        {
            SizeWarning.Message = $"Some pointers in this set only go up to {smallest} px, and your pointer size is {pointerSize} px. Windows will stretch them.";
            SizeWarning.IsOpen = true;
        }
    }

    private void OnApplySet(object sender, RoutedEventArgs e)
    {
        if (_set is null)
            return;
        try
        {
            App.Cursors.ApplySet(_set);
        }
        catch (Exception ex)
        {
            App.MainWindow.ShowError($"Couldn't apply {_set.Name}", ex);
        }
    }

    private void ApplyRole(CursorRole role)
    {
        if (_set is null)
            return;
        try
        {
            App.Cursors.ApplySingle(_set, role);
        }
        catch (Exception ex)
        {
            App.MainWindow.ShowError($"Couldn't apply {CursorRoles.Info(role).DisplayName}", ex);
        }
    }

    private async void OnRename(object sender, RoutedEventArgs e)
    {
        if (_set is null)
            return;
        var name = await Dialogs.AskForNameAsync(XamlRoot, "Rename set", "Choose a new name for this set.", _set.Name, "Rename");
        if (name is null || name == _set.Name)
            return;
        try
        {
            App.Cursors.Rename(_set, name);
        }
        catch (Exception ex)
        {
            App.MainWindow.ShowError("Couldn't rename the set", ex);
        }
    }

    private async void OnDelete(object sender, RoutedEventArgs e)
    {
        if (_set is null)
            return;
        if (App.Cursors.IsInUse(_set))
        {
            App.MainWindow.ShowMessage($"{_set.Name} is in use", "Apply a different set or reset to the Windows default first, then delete it.", InfoBarSeverity.Warning);
            return;
        }
        if (!await Dialogs.ConfirmAsync(XamlRoot, $"Delete {_set.Name}?", "The set and its cursor files are removed from PointerParlor. This can't be undone.", "Delete"))
            return;
        try
        {
            var set = _set;
            _setId = null;
            App.Cursors.Delete(set);
        }
        catch (Exception ex)
        {
            _setId = _set.Id;
            App.MainWindow.ShowError("Couldn't delete the set", ex);
        }
    }
}
