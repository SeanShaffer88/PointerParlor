using System.Text.Json;
using PointerParlor.Core;
using PointerParlor.Core.Imaging;
using SkiaSharp;

namespace PointerParlor.SetForge;

/// <summary>An openly licensed set: where it comes from and how it may be shared.</summary>
public sealed record OpenSet(
    string Id, string Name, string Author, string License, string SourceUrl, string Description,
    string CloneDir, string LicenseFile, Func<string, ICursorSource> Source);

/// <summary>
/// Converts openly licensed Linux cursor themes into Windows sets under sets/. Each set keeps its
/// license file and credits, as GPL, LGPL and CC BY-SA require. Clones live outside the repo; pass
/// their parent folder (each repo cloned as owner_repo).
/// </summary>
public static class OpenSets
{
    private static readonly int[] Sizes = [32, 48, 64, 96, 128];

    // Converted animations are full-color frames that compress poorly; skipping 128 px roughly halves them.
    // At the largest pointer size Windows scales the 96 px frames up.
    private static readonly int[] AnimatedSizes = [32, 48, 64, 96];
    private const int PngFromSize = 48;
    private const int SetVersion = 1;

    private static OpenSet Xcur(string id, string name, string author, string license, string url, string desc, string clone, string licenseFile, string themeDir) =>
        new(id, name, author, license, url, desc, clone, licenseFile, root => new XcursorThemeSource(Path.Combine(root, clone, themeDir, "cursors")));

    private static OpenSet Svg(string id, string name, string author, string url, string desc, string clone, string variant) =>
        new(id, name, author, "GPL-3.0", url, desc, clone, "LICENSE", root =>
        {
            var repo = Path.Combine(root, clone);
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(repo, "render.json")));
            var v = doc.RootElement.GetProperty(variant);
            // A variant without "colors" uses the SVGs as drawn (Banana's original yellow).
            var colors = v.TryGetProperty("colors", out var list)
                ? list.EnumerateArray().Select(c => (c.GetProperty("match").GetString()!, c.GetProperty("replace").GetString()!)).ToList()
                : [];
            var configs = Directory.Exists(Path.Combine(repo, "configs", "normal")) ? Path.Combine(repo, "configs", "normal") : Path.Combine(repo, "configs");
            return new SvgPackSource(Path.Combine(repo, v.GetProperty("dir").GetString()!), colors,
                Path.Combine(configs, "x.build.toml"), Path.Combine(configs, "win_rg.build.toml"));
        });

    private static List<OpenSet>? _all;

    /// <summary>Built on first use, after <see cref="Root"/> is set, since some packs list variants from their clone.</summary>
    public static IReadOnlyList<OpenSet> All => _all ??= BuildCatalog();

    private static List<OpenSet> BuildCatalog()
    {
        const string BibataUrl = "https://github.com/ful1e5/Bibata_Cursor";
        const string Kaiz = "Abdulkaiz Khatri (ful1e5)";
        var list = new List<OpenSet>
        {
            Svg("bibata-modern-amber", "Bibata Modern Amber", Kaiz, BibataUrl, "Rounded material-style pointers in amber with white and navy.", "ful1e5_Bibata_Cursor", "Bibata-Modern-Amber"),
            Svg("bibata-modern-classic", "Bibata Modern Classic", Kaiz, BibataUrl, "Rounded material-style pointers in black with a white edge.", "ful1e5_Bibata_Cursor", "Bibata-Modern-Classic"),
            Svg("bibata-modern-ice", "Bibata Modern Ice", Kaiz, BibataUrl, "Rounded material-style pointers in white with a black edge.", "ful1e5_Bibata_Cursor", "Bibata-Modern-Ice"),
            Svg("bibata-original-amber", "Bibata Original Amber", Kaiz, BibataUrl, "Sharp-tipped material-style pointers in amber with white and navy.", "ful1e5_Bibata_Cursor", "Bibata-Original-Amber"),
            Svg("bibata-original-classic", "Bibata Original Classic", Kaiz, BibataUrl, "Sharp-tipped material-style pointers in black with a white edge.", "ful1e5_Bibata_Cursor", "Bibata-Original-Classic"),
            Svg("bibata-original-ice", "Bibata Original Ice", Kaiz, BibataUrl, "Sharp-tipped material-style pointers in white with a black edge.", "ful1e5_Bibata_Cursor", "Bibata-Original-Ice"),
        };

        foreach (var (variant, desc) in new[] { ("Fuchsia", "magenta"), ("Fuchsia-Pop", "peach"), ("Fuchsia-Red", "red"), ("Fuchsia-Amber", "amber") })
            list.Add(Svg(Slug(variant), variant.Replace('-', ' '), Kaiz, "https://github.com/ful1e5/fuchsia-cursor", $"Bold, playful pointers in {desc} and white.", "ful1e5_fuchsia-cursor", variant));
        foreach (var variant in Variants("ful1e5_banana-cursor"))
            list.Add(Svg(Slug(variant), variant.Replace('-', ' '), Kaiz, "https://github.com/ful1e5/banana-cursor", "Chunky, friendly pointers with a banana-yellow body.", "ful1e5_banana-cursor", variant));
        foreach (var variant in Variants("ful1e5_XCursor-pro"))
            list.Add(Svg(Slug(variant), variant.Replace('-', ' '), Kaiz, "https://github.com/ful1e5/XCursor-pro", "Modern take on the classic X11 cursors.", "ful1e5_XCursor-pro", variant));

        const string Vince = "vinceliuice";
        list.AddRange(
        [
            Xcur("graphite-dark", "Graphite Dark", Vince, "GPL-3.0", "https://github.com/vinceliuice/Graphite-cursors", "Clean flat pointers in graphite gray.", "vinceliuice_Graphite-cursors", "LICENSE", "dist-dark"),
            Xcur("graphite-light", "Graphite Light", Vince, "GPL-3.0", "https://github.com/vinceliuice/Graphite-cursors", "Clean flat pointers in light gray.", "vinceliuice_Graphite-cursors", "LICENSE", "dist-light"),
            Xcur("graphite-dark-nord", "Graphite Dark Nord", Vince, "GPL-3.0", "https://github.com/vinceliuice/Graphite-cursors", "Clean flat pointers in Nord-palette dark slate.", "vinceliuice_Graphite-cursors", "LICENSE", "dist-dark-nord"),
            Xcur("graphite-light-nord", "Graphite Light Nord", Vince, "GPL-3.0", "https://github.com/vinceliuice/Graphite-cursors", "Clean flat pointers in Nord-palette snow.", "vinceliuice_Graphite-cursors", "LICENSE", "dist-light-nord"),
            Xcur("layan", "Layan", Vince, "GPL-3.0", "https://github.com/vinceliuice/Layan-cursors", "Soft pointers with a colorful gradient spinner.", "vinceliuice_Layan-cursors", "LICENSE", "dist"),
            Xcur("layan-border", "Layan Border", Vince, "GPL-3.0", "https://github.com/vinceliuice/Layan-cursors", "Layan with a bold contrasting border.", "vinceliuice_Layan-cursors", "LICENSE", "dist-border"),
            Xcur("layan-white", "Layan White", Vince, "GPL-3.0", "https://github.com/vinceliuice/Layan-cursors", "Layan in white.", "vinceliuice_Layan-cursors", "LICENSE", "dist-white"),
            Xcur("vimix", "Vimix", Vince, "GPL-3.0", "https://github.com/vinceliuice/Vimix-cursors", "Material-inspired pointers in dark gray.", "vinceliuice_Vimix-cursors", "LICENSE", "dist"),
            Xcur("vimix-white", "Vimix White", Vince, "GPL-3.0", "https://github.com/vinceliuice/Vimix-cursors", "Material-inspired pointers in white.", "vinceliuice_Vimix-cursors", "LICENSE", "dist-white"),
            Xcur("future", "Future", "yeyushengfan258", "GPL-3.0", "https://github.com/yeyushengfan258/Future-cursors", "Sleek pointers with a futuristic spinner.", "yeyushengfan258_Future-cursors", "LICENSE", "dist"),
        ]);

        const string CapUrl = "https://github.com/keeferrourke/capitaine-cursors";
        foreach (var tone in new[] { "dark", "light" })
        {
            list.Add(new($"capitaine-{tone}", $"Capitaine {Title(tone)}", "Keefer Rourke", "LGPL-3.0", CapUrl,
                $"macOS-inspired pointers based on KDE Breeze, {tone} variant.", "keeferrourke_capitaine-cursors", "COPYING",
                root => new SvgSpecSource(Path.Combine(root, "keeferrourke_capitaine-cursors", "src", "svg", tone), "",
                    SvgSpecSource.CapitaineSpecs(Path.Combine(root, "keeferrourke_capitaine-cursors", "src", "config")))));
        }

        const string VolUrl = "https://github.com/varlesh/volantes-cursors";
        foreach (var (folder, title, desc) in new[] { ("volantes_cursors", "Volantes", "dark"), ("volantes_light_cursors", "Volantes Light", "light") })
        {
            list.Add(new(CursorLibraryId(title), title, "Alexey Varfolomeev (varlesh)", "GPL-2.0", VolUrl,
                $"Material-flavored pointers with a smooth spinner, {desc} variant.", "varlesh_volantes-cursors", "LICENSE",
                root => new SvgSpecSource(Path.Combine(root, "varlesh_volantes-cursors", "src", folder), "_24",
                    SvgSpecSource.XcursorgenSpecs(Path.Combine(root, "varlesh_volantes-cursors", "src", "config")))));
        }

        const string Nordzy = "Guillaume Boehm (guillaumeboehm)";
        const string NordzyUrl = "https://github.com/guillaumeboehm/Nordzy-cursors";
        list.Add(Xcur("nordzy", "Nordzy", Nordzy, "GPL-3.0", NordzyUrl, "Pointers in the Nord palette's dark slate.", "guillaumeboehm_Nordzy-cursors", "COPYING", "xcursors/Nordzy-cursors"));
        list.Add(Xcur("nordzy-white", "Nordzy White", Nordzy, "GPL-3.0", NordzyUrl, "Pointers in the Nord palette's snow white.", "guillaumeboehm_Nordzy-cursors", "COPYING", "xcursors/Nordzy-cursors-white"));
        foreach (var flavor in new[] { "latte", "frappe", "macchiato", "mocha" })
        {
            foreach (var tone in new[] { "dark", "light" })
            {
                var title = $"Nordzy Catppuccin {Title(flavor)} {Title(tone)}";
                list.Add(Xcur($"nordzy-catppuccin-{flavor}-{tone}", title, Nordzy, "GPL-3.0", NordzyUrl,
                    $"Nordzy shapes in Catppuccin {Title(flavor)} colors, {tone} variant.", "guillaumeboehm_Nordzy-cursors", "COPYING", $"xcursors/Nordzy-catppuccin-{flavor}-{tone}"));
            }
        }
        return list;
    }

    private static IEnumerable<string> Variants(string clone)
    {
        var path = Path.Combine(Root ?? "", clone, "render.json");
        if (!File.Exists(path))
            return [];
        using var doc = JsonDocument.Parse(File.ReadAllText(path));
        return doc.RootElement.EnumerateObject().Select(p => p.Name).Where(n => !n.EndsWith("-Right", StringComparison.Ordinal)).ToList();
    }

    /// <summary>Set before <see cref="All"/> is first read so packs can list their variants.</summary>
    public static string? Root { get; set; }

    private static string Slug(string s) => s.ToLowerInvariant();
    private static string CursorLibraryId(string name) => PointerParlor.Core.Library.CursorLibrary.Slug(name);
    private static string Title(string s) => char.ToUpperInvariant(s[0]) + s[1..];

    public static void Run(string cloneRoot, string repo, string? only)
    {
        foreach (var set in All.Where(s => only is null || s.Id == only))
        {
            var cloneDir = Path.Combine(cloneRoot, set.CloneDir);
            if (!Directory.Exists(cloneDir))
            {
                Console.WriteLine($"{set.Name,-34} skipped: clone {set.CloneDir} not found");
                continue;
            }
            var source = set.Source(cloneRoot);
            var dir = Path.Combine(repo, "sets", set.Id);
            if (Directory.Exists(dir))
                Directory.Delete(dir, recursive: true);
            Directory.CreateDirectory(dir);

            var cursors = new Dictionary<string, string>();
            var fallbacks = new List<string>();
            foreach (var info in CursorRoles.All)
            {
                var frames = source.Frames(info.Role);
                if (frames is null && LinuxNames.Fallback(info.Role) is { } fb)
                {
                    frames = source.Frames(fb);
                    fallbacks.Add(info.DisplayName);
                }
                if (frames is null)
                    continue;
                var file = info.RegistryName + (frames.Count > 1 ? ".ani" : ".cur");
                File.WriteAllBytes(Path.Combine(dir, file), Write(frames, set, info));
                cursors[info.RegistryName] = file;
            }

            File.Copy(Path.Combine(cloneDir, set.LicenseFile), Path.Combine(dir, "LICENSE.txt"));
            var manifest = new
            {
                id = set.Id,
                name = set.Name,
                kind = "OpenLicense",
                author = set.Author,
                license = set.License,
                sourceUrl = set.SourceUrl,
                description = set.Description,
                version = SetVersion,
                cursors,
            };
            File.WriteAllText(Path.Combine(dir, "set.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n");
            WriteSheet(dir, cursors, Path.Combine(repo, "docs", "sets", "open", set.Id + ".png"));
            var note = fallbacks.Count > 0 ? $" ({string.Join(", ", fallbacks)} use Normal Select)" : "";
            Console.WriteLine($"{set.Name,-34} {cursors.Count,2} pointers {Directory.EnumerateFiles(dir).Sum(f => new FileInfo(f).Length) / 1024,6} KB{note}");
        }
    }

    private static byte[] Write(IReadOnlyList<SourceFrame> frames, OpenSet set, CursorRoleInfo info)
    {
        var sizes = frames.Count > 1 ? AnimatedSizes : Sizes;
        byte[] Cur(SourceFrame f) => CursorWriter.WriteCur(sizes.Select(w =>
        {
            var (bgra, hx, hy) = f.Render(w);
            return new CursorFrameSource(w, w, hx, hy, bgra);
        }).ToList(), PngFromSize);

        if (frames.Count == 1)
            return Cur(frames[0]);
        return CursorWriter.WriteAni(frames.Select(Cur).ToList(),
            frames.Select(f => Math.Max(1, (int)Math.Round(f.DelayMs * 60 / 1000.0))).ToList(),
            title: $"{set.Name}: {info.DisplayName}", artist: set.Author);
    }

    /// <summary>Preview sheet of the converted files at 48 px on light and dark rows, read back through Core's decoder.</summary>
    private static void WriteSheet(string dir, Dictionary<string, string> cursors, string path)
    {
        const int Cell = 64, Size = 48, Pad = 8;
        var width = Cell * CursorRoles.All.Count + Pad * 2;
        using var surface = SKSurface.Create(new SKImageInfo(width, Cell * 2 + Pad * 2));
        var canvas = surface.Canvas;
        canvas.Clear(new SKColor(0xF3, 0xF4, 0xF6));
        using (var dark = new SKPaint { Color = new SKColor(0x20, 0x22, 0x26) })
            canvas.DrawRect(0, Pad + Cell, width, Cell + Pad, dark);
        foreach (var info in CursorRoles.All)
        {
            if (!cursors.TryGetValue(info.RegistryName, out var file))
                continue;
            var asset = CursorReader.ReadFile(Path.Combine(dir, file));
            var frame = asset.Frames[asset.Steps[asset.Steps.Count / 3].FrameIndex].BestFor(Size);
            using var data = SKData.CreateCopy(frame.ToPng());
            using var image = SKImage.FromEncodedData(data);
            var x = Pad + (int)info.Role * Cell + (Cell - Size) / 2;
            canvas.DrawImage(image, new SKRect(x, Pad + (Cell - Size) / 2, x + Size, Pad + (Cell - Size) / 2 + Size));
            canvas.DrawImage(image, new SKRect(x, Pad + Cell + (Cell - Size) / 2, x + Size, Pad + Cell + (Cell - Size) / 2 + Size));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using var png = surface.Snapshot().Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, png.ToArray());
    }
}
