using PointerParlor.Core.Imaging;
using PointerParlor.Core.Library;

namespace PointerParlor.Core.Tests;

/// <summary>Checks the sets committed under sets/ (drawn by tools/SetForge) are complete and readable.</summary>
public class BundledSetTests
{
    private static readonly string[] SignatureIds =
        ["velvet", "lagoon", "brass", "ink", "chalk", "pixel", "glass", "candy", "blueprint", "phosphor", "neon", "beacon-xl"];

    private static readonly int[] Sizes = [32, 48, 64, 96, 128];

    private static string SetsRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "PointerParlor.sln")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return Path.Combine(dir.FullName, "sets");
    }

    public static TheoryData<string> Ids() => new(SignatureIds);

    [Fact]
    public void All_twelve_signature_sets_install_into_a_library()
    {
        using var tmp = new TempDir();
        var lib = new CursorLibrary(tmp.Path);

        var installed = lib.InstallBundled(SetsRoot());
        var (sets, problems) = lib.LoadAll();

        Assert.Equal(SignatureIds.Order(), installed.Order());
        Assert.Empty(problems);
        Assert.All(sets, s =>
        {
            Assert.Equal(SetKind.Signature, s.Kind);
            Assert.True(s.IsComplete, $"{s.Name} is missing pointers");
            Assert.False(string.IsNullOrWhiteSpace(s.Manifest.Description));
            Assert.Equal("MIT", s.Manifest.License);
        });
    }

    [Theory]
    [MemberData(nameof(Ids))]
    public void Every_pointer_decodes_at_every_size_with_a_hotspot_inside(string id)
    {
        var manifest = CursorLibrary.ReadManifest(Path.Combine(SetsRoot(), id, "set.json"), out var error);
        Assert.True(manifest is not null, error);
        var set = new CursorSet(manifest, Path.Combine(SetsRoot(), id));

        foreach (var info in CursorRoles.All)
        {
            var asset = CursorReader.ReadFile(set.Paths[info.Role]);
            Assert.Equal(info.UsuallyAnimated, asset.IsAnimated);
            foreach (var frame in asset.Frames)
            {
                Assert.Equal(Sizes, frame.Images.Select(i => i.Width));
                Assert.All(frame.Images, img =>
                {
                    Assert.Equal(img.Width, img.Height);
                    Assert.InRange(img.HotspotX, 0, img.Width - 1);
                    Assert.InRange(img.HotspotY, 0, img.Height - 1);
                });
            }
        }
    }
}
