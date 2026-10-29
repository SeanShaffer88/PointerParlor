using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PointerParlor.Services;
using Windows.UI.ViewManagement;

namespace PointerParlor.Controls;

/// <summary>
/// Shows a cursor file at a given size. Animated cursors play when <see cref="Animate"/> is on and
/// Windows animations are enabled; otherwise the first frame is shown.
/// </summary>
public sealed class CursorPreview : Grid
{
    public static readonly DependencyProperty PathProperty = DependencyProperty.Register(
        nameof(Path), typeof(string), typeof(CursorPreview), new PropertyMetadata(null, (d, _) => ((CursorPreview)d).Reload()));

    public static readonly DependencyProperty DisplaySizeProperty = DependencyProperty.Register(
        nameof(DisplaySize), typeof(double), typeof(CursorPreview), new PropertyMetadata(32.0, (d, _) => ((CursorPreview)d).Reload()));

    public static readonly DependencyProperty AnimateProperty = DependencyProperty.Register(
        nameof(Animate), typeof(bool), typeof(CursorPreview), new PropertyMetadata(false, (d, _) => ((CursorPreview)d).Restart()));

    private static readonly UISettings SystemSettings = new();

    private readonly Image _image = new() { Stretch = Stretch.Uniform };
    private readonly DispatcherTimer _timer = new();
    private CursorPreviewData? _data;
    private int _step;
    private int _loadVersion;

    public CursorPreview()
    {
        Children.Add(_image);
        _timer.Tick += OnTick;
        Loaded += (_, _) => Restart();
        Unloaded += (_, _) => _timer.Stop();
    }

    public string? Path
    {
        get => (string?)GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public double DisplaySize
    {
        get => (double)GetValue(DisplaySizeProperty);
        set => SetValue(DisplaySizeProperty, value);
    }

    public bool Animate
    {
        get => (bool)GetValue(AnimateProperty);
        set => SetValue(AnimateProperty, value);
    }

    private async void Reload()
    {
        Width = Height = DisplaySize;
        var version = ++_loadVersion;
        _timer.Stop();
        _data = null;
        _image.Source = null;
        if (string.IsNullOrEmpty(Path))
            return;

        var scale = XamlRoot?.RasterizationScale ?? 1.0;
        var data = await PreviewLoader.LoadAsync(Path, (int)Math.Ceiling(DisplaySize * scale));
        if (version != _loadVersion || data is null)
            return;
        _data = data;
        Restart();
    }

    private void Restart()
    {
        _timer.Stop();
        _step = 0;
        if (_data is null)
            return;
        _image.Source = _data.Steps[0].Image;
        if (Animate && _data.Steps.Count > 1 && SystemSettings.AnimationsEnabled && IsLoaded)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(16, _data.Steps[0].DurationMs));
            _timer.Start();
        }
    }

    private void OnTick(object? sender, object e)
    {
        if (_data is null)
        {
            _timer.Stop();
            return;
        }
        _step = (_step + 1) % _data.Steps.Count;
        var (image, duration) = _data.Steps[_step];
        _image.Source = image;
        _timer.Interval = TimeSpan.FromMilliseconds(Math.Max(16, duration));
    }
}
