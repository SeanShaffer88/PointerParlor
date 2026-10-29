using System.Text.RegularExpressions;
using PointerParlor.Core;
using PointerParlor.Core.Imaging;
using SkiaSharp;

namespace PointerParlor.SetForge;

/// <summary>One frame of a converted pointer: renders itself at a Windows size, with its display time.</summary>
public sealed record SourceFrame(Func<int, (byte[] Bgra, int HotX, int HotY)> Render, int DelayMs);

/// <summary>Where an openly licensed set's artwork comes from.</summary>
public interface ICursorSource
{
    /// <summary>The frames for a role, or null when the theme has no pointer for it.</summary>
    IReadOnlyList<SourceFrame>? Frames(CursorRole role);
}

/// <summary>Linux cursor names for each Windows role, most specific first.</summary>
public static class LinuxNames
{
    public static readonly IReadOnlyDictionary<CursorRole, string[]> ForRole = new Dictionary<CursorRole, string[]>
    {
        [CursorRole.Arrow] = ["left_ptr", "default", "arrow"],
        [CursorRole.Help] = ["help", "question_arrow", "whats_this", "left_ptr_help"],
        [CursorRole.AppStarting] = ["progress", "left_ptr_watch", "half-busy"],
        [CursorRole.Wait] = ["wait", "watch"],
        [CursorRole.Crosshair] = ["crosshair", "cross", "tcross"],
        [CursorRole.IBeam] = ["text", "xterm", "ibeam"],
        [CursorRole.NWPen] = ["pencil", "draft"],
        [CursorRole.No] = ["not-allowed", "circle", "crossed_circle", "forbidden", "no-drop"],
        [CursorRole.SizeNS] = ["ns-resize", "size_ver", "sb_v_double_arrow", "v_double_arrow", "row-resize"],
        [CursorRole.SizeWE] = ["ew-resize", "size_hor", "sb_h_double_arrow", "h_double_arrow", "col-resize"],
        [CursorRole.SizeNWSE] = ["nwse-resize", "size_fdiag", "bd_double_arrow", "bottom_right_corner", "top_left_corner"],
        [CursorRole.SizeNESW] = ["nesw-resize", "size_bdiag", "fd_double_arrow", "bottom_left_corner", "top_right_corner"],
        // "move" is a grab hand in some themes, so the four-way arrow names come first.
        [CursorRole.SizeAll] = ["size_all", "fleur", "all-scroll", "move"],
        [CursorRole.UpArrow] = ["up-arrow", "up_arrow", "center_ptr", "sb_up_arrow"],
        [CursorRole.Hand] = ["pointer", "hand2", "pointing_hand", "hand1"],
        [CursorRole.Pin] = ["pin", "location"],
        [CursorRole.Person] = ["person"],
    };

    /// <summary>Linux themes rarely draw Pin and Person; those fall back to the theme's own Normal Select.</summary>
    public static CursorRole? Fallback(CursorRole role) => role is CursorRole.Pin or CursorRole.Person ? CursorRole.Arrow : null;
}

/// <summary>A built Linux cursor theme (a folder of Xcursor files).</summary>
public sealed class XcursorThemeSource(string cursorsDir) : ICursorSource
{
    // Linux nominal 24 is the everyday size, which Windows calls 32.
    private const float NominalPerWindowsPixel = 0.75f;

    public IReadOnlyList<SourceFrame>? Frames(CursorRole role)
    {
        var file = LinuxNames.ForRole[role].Select(n => Path.Combine(cursorsDir, n)).FirstOrDefault(File.Exists);
        if (file is null)
            return null;
        var bySize = XcursorReader.ReadFile(file);
        var count = bySize.Values.Max(v => v.Count);
        // Long animations are thinned to keep files small; each kept frame shows for the frames it replaces.
        var step = (int)Math.Ceiling(count / (double)SvgPackSource.MaxFrames);
        var timing = bySize.Values.First(v => v.Count == count);
        return Enumerable.Range(0, count).Where(i => i % step == 0).Select(i =>
        {
            var delay = timing.Skip(i).Take(step).Sum(img => Math.Max(1, img.DelayMs));
            return new SourceFrame(w => RenderAt(bySize, i, w), delay);
        }).ToList();
    }

    private static (byte[], int, int) RenderAt(IReadOnlyDictionary<int, IReadOnlyList<XcursorImage>> bySize, int index, int w)
    {
        var wantNominal = w * NominalPerWindowsPixel;
        // Prefer the smallest nominal size at least as large as wanted (downscaling stays sharp), else the largest.
        var nominal = bySize.Keys.Where(n => n >= wantNominal).DefaultIfEmpty(bySize.Keys.Max()).Min();
        var frames = bySize[nominal];
        var img = frames[index % frames.Count];
        var scale = wantNominal / nominal;
        return Raster.Place(img.Bgra, img.Width, img.Height, img.HotspotX, img.HotspotY, scale, w);
    }
}

/// <summary>
/// SVG packs in the layout used by Bibata, Fuchsia, Banana and XCursor-Pro: one SVG per Linux cursor
/// name (or a folder of numbered frames), placeholder colors swapped per variant, hotspots in x.build.toml.
/// </summary>
public sealed class SvgPackSource : ICursorSource
{
    // The packs' "Large" Windows preset draws a 26 px image into a 32 px cursor, close to Windows' own pointer size.
    private const float ImagePerWindowsPixel = 26f / 32f;
    public const int MaxFrames = 30;

    private readonly string _svgDir;
    private readonly IReadOnlyList<(string Match, string Replace)> _colors;
    private readonly Dictionary<string, Dictionary<string, string>> _config;

    private readonly Dictionary<CursorRole, string> _authorMapping = [];

    // Role names in the packs' Windows build configs.
    private static readonly Dictionary<string, CursorRole> WinNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Pointer"] = CursorRole.Arrow, ["Help"] = CursorRole.Help, ["Work"] = CursorRole.AppStarting, ["Busy"] = CursorRole.Wait,
        ["Cross"] = CursorRole.Crosshair, ["Text"] = CursorRole.IBeam, ["Handwriting"] = CursorRole.NWPen, ["Unavailable"] = CursorRole.No,
        ["Vert"] = CursorRole.SizeNS, ["Horz"] = CursorRole.SizeWE, ["Dgn1"] = CursorRole.SizeNWSE, ["Dgn2"] = CursorRole.SizeNESW,
        ["Move"] = CursorRole.SizeAll, ["Alternate"] = CursorRole.UpArrow, ["Link"] = CursorRole.Hand, ["Pin"] = CursorRole.Pin, ["Person"] = CursorRole.Person,
    };

    /// <param name="winToml">The pack's own Windows build config; its win_name choices take priority over the generic name table.</param>
    public SvgPackSource(string svgDir, IReadOnlyList<(string, string)> colors, string buildToml, string? winToml)
    {
        _svgDir = svgDir;
        _colors = colors;
        _config = MiniToml.Sections(File.ReadAllText(buildToml));
        if (winToml is not null && File.Exists(winToml))
        {
            foreach (var (section, values) in MiniToml.Sections(File.ReadAllText(winToml)))
            {
                if (section.StartsWith("cursors.", StringComparison.Ordinal) && values.TryGetValue("win_name", out var winName) && WinNames.TryGetValue(winName, out var r))
                    _authorMapping.TryAdd(r, section["cursors.".Length..]);
            }
        }
    }

    public IReadOnlyList<SourceFrame>? Frames(CursorRole role)
    {
        var names = _authorMapping.TryGetValue(role, out var mapped) ? [mapped, .. LinuxNames.ForRole[role]] : LinuxNames.ForRole[role];
        foreach (var name in names)
        {
            var file = Path.Combine(_svgDir, name + ".svg");
            var dir = Path.Combine(_svgDir, name);
            List<string> files;
            if (File.Exists(file))
                files = [file];
            else if (Directory.Exists(dir))
                files = Directory.GetFiles(dir, "*.svg").Order(StringComparer.Ordinal).ToList();
            else
                continue;
            if (files.Count == 0)
                continue;

            var settings = _config.GetValueOrDefault("cursors." + name) ?? [];
            var fallback = _config.GetValueOrDefault("cursors.fallback_settings") ?? [];
            int Setting(string key, int dflt) =>
                int.TryParse(settings.GetValueOrDefault(key) ?? fallback.GetValueOrDefault(key), out var v) ? v : dflt;
            var hx = Setting("x_hotspot", 128);
            var hy = Setting("y_hotspot", 128);
            var delay = Setting("x11_delay", 30);

            // Long animations are thinned to keep files small; the cycle time stays the same.
            var step = (int)Math.Ceiling(files.Count / (double)MaxFrames);
            return files.Where((_, i) => i % step == 0)
                .Select(f => new SourceFrame(w => RenderSvg(f, hx, hy, w), delay * step))
                .ToList();
        }
        return null;
    }

    private (byte[], int, int) RenderSvg(string file, int hx, int hy, int w)
    {
        var text = File.ReadAllText(file);
        foreach (var (match, replace) in _colors)
            text = Regex.Replace(text, Regex.Escape(match), replace, RegexOptions.IgnoreCase);
        using var svg = new Svg.Skia.SKSvg();
        using var picture = svg.FromSvg(text) ?? throw new InvalidOperationException($"Could not read {file}.");
        var viewBox = picture.CullRect.Width > 0 ? picture.CullRect.Width : 256;
        var size = (int)MathF.Round(w * ImagePerWindowsPixel);
        var scale = size / viewBox;
        return Raster.Picture(picture, scale, w, hx * scale, hy * scale);
    }
}

/// <summary>Pixel helpers shared by the sources.</summary>
public static class Raster
{
    /// <summary>Draws straight-alpha BGRA pixels scaled into a w×w canvas at the top-left; returns pixels and the scaled hotspot.</summary>
    public static (byte[], int, int) Place(byte[] bgra, int width, int height, int hx, int hy, float scale, int w)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var src = new SKBitmap(info);
        System.Runtime.InteropServices.Marshal.Copy(bgra, 0, src.GetPixels(), bgra.Length);
        using var image = SKImage.FromBitmap(src);
        using var surface = SKSurface.Create(new SKImageInfo(w, w, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        var sampling = Math.Abs(scale - 1) < 0.001f
            ? new SKSamplingOptions(SKFilterMode.Nearest)
            : new SKSamplingOptions(SKCubicResampler.Mitchell);
        surface.Canvas.DrawImage(image, new SKRect(0, 0, width * scale, height * scale), sampling);
        return (Read(surface, w), Clamp(hx * scale, w), Clamp(hy * scale, w));
    }

    public static (byte[], int, int) Picture(SKPicture picture, float scale, int w, float hx, float hy)
    {
        using var surface = SKSurface.Create(new SKImageInfo(w, w, SKColorType.Bgra8888, SKAlphaType.Premul));
        surface.Canvas.Clear(SKColors.Transparent);
        surface.Canvas.Scale(scale);
        surface.Canvas.DrawPicture(picture);
        return (Read(surface, w), Clamp(hx, w), Clamp(hy, w));
    }

    private static int Clamp(float v, int w) => Math.Clamp((int)MathF.Floor(v), 0, w - 1);

    private static byte[] Read(SKSurface surface, int w)
    {
        using var snapshot = surface.Snapshot();
        var info = new SKImageInfo(w, w, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(info);
        snapshot.ReadPixels(info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
        return bitmap.Bytes;
    }
}

/// <summary>Just enough TOML for the packs' build configs: [section] headers and scalar key = value lines.</summary>
public static class MiniToml
{
    public static Dictionary<string, Dictionary<string, string>> Sections(string text)
    {
        var result = new Dictionary<string, Dictionary<string, string>>();
        var current = new Dictionary<string, string>();
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Split('#')[0].Trim();
            if (line.StartsWith('[') && line.EndsWith(']'))
            {
                result[line.Trim('[', ']').Trim()] = current = [];
                continue;
            }
            var eq = line.IndexOf('=');
            if (eq > 0)
                current[line[..eq].Trim()] = line[(eq + 1)..].Trim().Trim('\'', '"');
        }
        return result;
    }
}
