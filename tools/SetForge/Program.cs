using System.Text.Json;
using PointerParlor.Core;
using PointerParlor.Core.Imaging;
using PointerParlor.SetForge;
using SkiaSharp;

// Draws every signature set into <repo>/sets and a preview sheet per set into <repo>/docs/sets.
// Usage: dotnet run -c Release --project tools/SetForge [-- <repo root>] [--only <id>]

var repo = args.FirstOrDefault(a => !a.StartsWith("--") && Directory.Exists(a)) ?? FindRepoRoot();
var only = args.SkipWhile(a => a != "--only").Skip(1).FirstOrDefault();
int[] sizes = [32, 48, 64, 96, 128];
const int PngFromSize = 64;   // larger images are stored PNG-compressed to keep files small
const int SpinnerFrames = 10;
const int SpinnerJiffies = 4; // 1/60 s per frame: one turn every ~0.67 s
const int SetVersion = 1;

var themes = Theme.All.Where(t => only is null || t.Id == only).ToList();
foreach (var theme in themes)
{
    var dir = Path.Combine(repo, "sets", theme.Id);
    if (Directory.Exists(dir))
        Directory.Delete(dir, recursive: true);
    Directory.CreateDirectory(dir);

    var cursors = new Dictionary<string, string>();
    foreach (var info in CursorRoles.All)
    {
        var seed = StableSeed(theme.Id, info.Role);
        string file;
        if (info.UsuallyAnimated)
        {
            var frames = Enumerable.Range(0, SpinnerFrames)
                .Select(i => Cur(Shapes.Build(info.Role, theme.Family, i / (float)SpinnerFrames), theme, seed))
                .ToList();
            file = info.RegistryName + ".ani";
            File.WriteAllBytes(Path.Combine(dir, file), CursorWriter.WriteAni(frames, Enumerable.Repeat(SpinnerJiffies, SpinnerFrames).ToList(),
                title: $"PointerParlor {theme.Name}: {info.DisplayName}", artist: "PointerParlor"));
        }
        else
        {
            file = info.RegistryName + ".cur";
            File.WriteAllBytes(Path.Combine(dir, file), Cur(Shapes.Build(info.Role, theme.Family), theme, seed));
        }
        cursors[info.RegistryName] = file;
    }

    var manifest = new
    {
        id = theme.Id,
        name = theme.Name,
        kind = "Signature",
        author = "PointerParlor",
        license = "MIT",
        description = theme.Description,
        version = SetVersion,
        cursors,
    };
    File.WriteAllText(Path.Combine(dir, "set.json"), JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }) + "\n");
    WriteSheet(theme, Path.Combine(repo, "docs", "sets", theme.Id + ".png"));
    Console.WriteLine($"{theme.Name,-10} {Directory.EnumerateFiles(dir).Sum(f => new FileInfo(f).Length) / 1024,6} KB");
}

byte[] Cur(Glyph glyph, Theme theme, int seed) =>
    CursorWriter.WriteCur(sizes.Select(size =>
    {
        var (bgra, hx, hy) = Renderer.Render(glyph, theme, size, seed);
        return new CursorFrameSource(size, size, hx, hy, bgra);
    }).ToList(), PngFromSize);

// Preview sheet: the 17 pointers at 48 px on a light row and a dark row.
static void WriteSheet(Theme theme, string path)
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
        var glyph = Shapes.Build(info.Role, theme.Family, 0.15f);
        var (bgra, _, _) = Renderer.Render(glyph, theme, Size, StableSeed(theme.Id, info.Role));
        using var bitmap = new SKBitmap(new SKImageInfo(Size, Size, SKColorType.Bgra8888, SKAlphaType.Unpremul));
        System.Runtime.InteropServices.Marshal.Copy(bgra, 0, bitmap.GetPixels(), bgra.Length);
        using var image = SKImage.FromBitmap(bitmap);
        var x = Pad + (int)info.Role * Cell + (Cell - Size) / 2;
        canvas.DrawImage(image, x, Pad + (Cell - Size) / 2);
        canvas.DrawImage(image, x, Pad + Cell + (Cell - Size) / 2);
    }
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var png = surface.Snapshot().Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllBytes(path, png.ToArray());
}

// string.GetHashCode is randomized per process; this keeps splatter identical between runs.
static int StableSeed(string id, CursorRole role)
{
    var h = 17;
    foreach (var c in id)
        h = h * 31 + c;
    return h * 31 + (int)role;
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PointerParlor.sln")))
        dir = dir.Parent;
    return dir?.FullName ?? throw new InvalidOperationException("Run from inside the PointerParlor repo, or pass the repo root.");
}
