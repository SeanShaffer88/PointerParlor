using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using PointerParlor.Core;
using PointerParlor.Core.Library;
using PointerParlor.Services;
using PointerParlor.Views;
using Windows.Graphics;

namespace PointerParlor;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon(Path.Combine(AppContext.BaseDirectory, "Assets", "PointerParlor.ico"));
        AppWindow.Resize(new SizeInt32(1200, 840));
        ApplyTheme(App.Settings.Theme);

        App.Cursors.Changed += (_, _) => UpdateStatus();
        Activated += (_, e) =>
        {
            // Cursors can change outside the app (Settings, Control Panel); re-read when returning.
            if (e.WindowActivationState != WindowActivationState.Deactivated)
                App.Cursors.RefreshActive();
        };
        UpdateStatus();
        ContentFrame.Navigate(typeof(GalleryPage));

        if (App.Cursors.LibraryProblems.Count > 0)
            ShowMessage("Some sets could not be loaded", string.Join(Environment.NewLine, App.Cursors.LibraryProblems), InfoBarSeverity.Warning);
    }

    public void ApplyTheme(ThemeChoice theme)
    {
        Root.RequestedTheme = theme switch
        {
            ThemeChoice.Light => ElementTheme.Light,
            ThemeChoice.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default,
        };
    }

    public void ShowError(string title, Exception ex)
    {
        AppLog.Error(title, ex);
        ShowMessage(title, ex.Message, InfoBarSeverity.Error);
    }

    public void ShowMessage(string title, string message, InfoBarSeverity severity = InfoBarSeverity.Informational)
    {
        MessageBar.Title = title;
        MessageBar.Message = message;
        MessageBar.Severity = severity;
        MessageBar.IsOpen = true;
    }

    public void NavigateToSet(string setId) => ContentFrame.Navigate(typeof(SetDetailPage), setId);

    private void UpdateStatus()
    {
        var active = App.Cursors.Active;
        StatusText.Text = active.Kind switch
        {
            ActiveKind.Set => $"In use: {active.Set!.Name}",
            ActiveKind.Mix => $"In use: unsaved mix of {active.Cursors.Count(c => c.Path is not null)} pointers",
            _ => "In use: Windows default cursors",
        };
        SaveMixButton.Visibility = active.Kind == ActiveKind.Mix ? Visibility.Visible : Visibility.Collapsed;
        UndoButton.IsEnabled = App.Cursors.CanUndo;
        UndoText.Text = App.Cursors.LastSnapshot is { } snapshot ? $"Undo: {snapshot.Description}" : "Undo";
        ToolTipService.SetToolTip(UndoButton, App.Cursors.CanUndo ? "Put back the cursors from before the last change" : "Nothing to undo");
    }

    private void OnUndo(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Cursors.Undo();
        }
        catch (Exception ex)
        {
            ShowError("Couldn't undo", ex);
        }
    }

    private async void OnSaveMix(object sender, RoutedEventArgs e)
    {
        var name = await Dialogs.AskForNameAsync(Content.XamlRoot, "Save mix", "Save your current pointers as a set in My sets.", "My mix", "Save");
        if (name is null)
            return;
        try
        {
            var set = App.Cursors.SaveMix(name);
            NavigateToSet(set.Id);
        }
        catch (Exception ex)
        {
            ShowError("Couldn't save the mix", ex);
        }
    }

    private void OnNavItemInvoked(NavigationView sender, NavigationViewItemInvokedEventArgs args)
    {
        if (args.IsSettingsInvoked)
            ContentFrame.Navigate(typeof(SettingsPage));
        else if (ContentFrame.CurrentSourcePageType != typeof(GalleryPage))
            ContentFrame.Navigate(typeof(GalleryPage));
    }

    private void OnBackRequested(NavigationView sender, NavigationViewBackRequestedEventArgs args)
    {
        if (ContentFrame.CanGoBack)
            ContentFrame.GoBack();
    }

    private void OnNavigated(object sender, NavigationEventArgs e)
    {
        Nav.IsBackEnabled = ContentFrame.CanGoBack;
        if (e.SourcePageType == typeof(SettingsPage))
            Nav.SelectedItem = Nav.SettingsItem;
        else
            Nav.SelectedItem = Nav.MenuItems[0];
    }
}
