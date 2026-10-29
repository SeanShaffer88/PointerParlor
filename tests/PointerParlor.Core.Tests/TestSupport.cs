using PointerParlor.Core.Imaging;
using PointerParlor.Core.Schemes;

namespace PointerParlor.Core.Tests;

/// <summary>Builds cursor files in memory and on disk for tests.</summary>
internal static class Fixtures
{
    /// <summary>A solid square with a transparent first row, so mask handling is visible.</summary>
    public static CursorFrameSource Square(int size, byte b, byte g, byte r, int hotX = 0, int hotY = 0)
    {
        var px = new byte[size * size * 4];
        for (var i = 0; i < size * size; i++)
        {
            var transparent = i < size; // first row
            px[i * 4] = b;
            px[i * 4 + 1] = g;
            px[i * 4 + 2] = r;
            px[i * 4 + 3] = transparent ? (byte)0 : (byte)255;
        }
        return new CursorFrameSource(size, size, hotX, hotY, px);
    }

    public static byte[] Cur(int size = 32, byte r = 255, int hotX = 3, int hotY = 4) =>
        CursorWriter.WriteCur([Square(size, 0, 0, r, hotX, hotY)]);

    public static byte[] Ani(int frames = 3, int jiffies = 6) =>
        CursorWriter.WriteAni(
            Enumerable.Range(0, frames).Select(i => Cur(32, (byte)(i * 40))).ToList(),
            Enumerable.Repeat(jiffies, frames).ToList(),
            title: "Spinner", artist: "Test Artist");
}

/// <summary>A temporary folder deleted when the test ends.</summary>
internal sealed class TempDir : IDisposable
{
    public TempDir()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "pp-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public string Path { get; }

    public string File(string relative, byte[] content)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllBytes(full, content);
        return full;
    }

    public string Text(string relative, string content) => File(relative, System.Text.Encoding.UTF8.GetBytes(content));

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); } catch (IOException) { }
    }
}

/// <summary>An in-memory HKCU for planner and snapshot tests.</summary>
internal sealed class FakeRegistry
{
    private readonly Dictionary<(string, string), RegValue> _values = [];

    public RegValue? Read(string key, string name) => _values.GetValueOrDefault((key.ToUpperInvariant(), name.ToUpperInvariant()));

    public void Set(string key, string name, RegValue value) => _values[(key.ToUpperInvariant(), name.ToUpperInvariant())] = value;

    public void Apply(IEnumerable<RegistryChange> changes)
    {
        foreach (var c in changes)
        {
            var k = (c.KeyPath.ToUpperInvariant(), c.ValueName.ToUpperInvariant());
            if (c.Value is null) _values.Remove(k);
            else _values[k] = c.Value;
        }
    }

    public int Count => _values.Count;
}
