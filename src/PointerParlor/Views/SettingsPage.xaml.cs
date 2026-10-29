using System.Diagnostics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using PointerParlor.Core;
using PointerParlor.Core.Schemes;

namespace PointerParlor.Views;

public sealed partial class SettingsPage : Page
{
    private readonly DispatcherTimer _sizeDebounce = new() { Interval = TimeSpan.FromMilliseconds(400) };
    private bool _loading = true; // the slider raises ValueChanged while the page is still being built

    public SettingsPage()
    {
        InitializeComponent();
        _sizeDebounce.Tick += (_, _) => CommitSize();
    }

    /// <summary>Windows' size slider steps: 1 = 32 px, each step adds 16 px, 15 = 256 px.</summary>
    private static int StepToPixels(double step) => SchemePlanner.MinPointerSize + ((int)Math.Round(step) - 1) * 16;

    private static double PixelsToStep(int pixels) => Math.Clamp((pixels - SchemePlanner.MinPointerSize) / 16 + 1, 1, 15);

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        _loading = true;
        SizeSlider.Value = PixelsToStep(App.Cursors.PointerSize);
        SizeLabel.Text = $"{App.Cursors.PointerSize} px";
        ThemeChoices.SelectedIndex = (int)App.Settings.Theme;
        _loading = false;

        var version = typeof(App).Assembly.GetName().Version;
        VersionText.Text = $"PointerParlor {version?.ToString(3)}";
        var notices = Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md");
        NoticesText.Text = File.Exists(notices) ? File.ReadAllText(notices) : "The notices file is missing from this install.";
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        if (_sizeDebounce.IsEnabled)
            CommitSize();
    }

    private void OnSizeChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (_loading)
            return;
        SizeLabel.Text = $"{StepToPixels(e.NewValue)} px";
        _sizeDebounce.Stop();
        _sizeDebounce.Start();
    }

    private void CommitSize()
    {
        _sizeDebounce.Stop();
        try
        {
            App.Cursors.SetPointerSize(StepToPixels(SizeSlider.Value));
        }
        catch (Exception ex)
        {
            App.MainWindow.ShowError("Couldn't change the pointer size", ex);
        }
    }

    private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_loading || ThemeChoices.SelectedIndex < 0)
            return;
        App.Settings.Theme = (ThemeChoice)ThemeChoices.SelectedIndex;
        App.Settings.Save();
        App.MainWindow.ApplyTheme(App.Settings.Theme);
    }

    private void OnReset(object sender, RoutedEventArgs e)
    {
        try
        {
            App.Cursors.ResetToDefault();
        }
        catch (Exception ex)
        {
            App.MainWindow.ShowError("Couldn't reset the cursors", ex);
        }
    }

    private void OnOpenLibrary(object sender, RoutedEventArgs e)
    {
        Directory.CreateDirectory(App.Paths.Library);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{App.Paths.Library}\"") { UseShellExecute = true });
    }
}
