using System.Buffers.Binary;
using System.IO.Compression;
using PointerParlor.Core.Imaging;

namespace PointerParlor.Core.Tests;

public class CurDecoderTests
{
    [Fact]
    public void RoundTrips_32bit_cursor_with_hotspot_and_alpha()
    {
        var file = CurDecoder.Decode(Fixtures.Cur(32, r: 200, hotX: 5, hotY: 9));

        var img = Assert.Single(file.Images);
        Assert.Equal((32, 32, 5, 9, 32), (img.Width, img.Height, img.HotspotX, img.HotspotY, img.BitDepth));
        Assert.False(img.IsPng);
        var px = img.Bgra;
        Assert.Equal(0, px[3]);                           // first row transparent
        Assert.Equal(new byte[] { 0, 0, 200, 255 }, px.Slice(32 * 4, 4).ToArray()); // second row opaque red
    }

    [Fact]
    public void BestFor_picks_smallest_image_at_least_the_size_else_largest()
    {
        var bytes = CursorWriter.WriteCur([Fixtures.Square(32, 0, 0, 0), Fixtures.Square(64, 0, 0, 0), Fixtures.Square(48, 0, 0, 0)]);
        var file = CurDecoder.Decode(bytes);

        Assert.Equal(48, file.BestFor(40).Width);
        Assert.Equal(32, file.BestFor(32).Width);
        Assert.Equal(64, file.BestFor(128).Width);
        Assert.Equal(64, file.LargestSize);
    }

    [Fact]
    public void Writer_stores_large_images_as_png_and_they_decode_back()
    {
        var bytes = CursorWriter.WriteCur([Fixtures.Square(32, 1, 2, 3, 4, 5), Fixtures.Square(64, 1, 2, 3, 9, 10)], pngFromSize: 64);
        var file = CurDecoder.Decode(bytes);

        Assert.False(file.Images[0].IsPng);
        var big = file.Images[1];
        Assert.True(big.IsPng);
        Assert.Equal((64, 64, 9, 10), (big.Width, big.Height, big.HotspotX, big.HotspotY));
    }

    [Fact]
    public void Passes_embedded_png_through_and_reads_its_size()
    {
        var png = PngWriter.EncodeBgra(256, 256, new byte[256 * 256 * 4]);
        var bytes = new byte[6 + 16 + png.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 1);
        // width/height bytes of 0 mean 256
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10), 12);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(12), 7);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), png.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), 22);
        png.CopyTo(bytes, 22);

        var img = Assert.Single(CurDecoder.Decode(bytes).Images);
        Assert.True(img.IsPng);
        Assert.Equal((256, 256, 12, 7), (img.Width, img.Height, img.HotspotX, img.HotspotY));
        Assert.Equal(png, img.ToPng());
    }

    [Fact]
    public void Decodes_monochrome_cursor_with_and_mask_and_inverted_pixels()
    {
        // 8x8, 1-bit: palette [black, white]. Row 0 (top): white opaque. Row 1: transparent. Row 2: inverting.
        const int w = 8, h = 8;
        var palette = new byte[] { 0, 0, 0, 0, 255, 255, 255, 0 };
        var xor = new byte[4 * h];
        var and = new byte[4 * h];
        xor[(h - 1) * 4] = 0xFF;                                   // top row: white
        and[(h - 2) * 4] = 0xFF;                                   // second row: mask set, color black => transparent
        xor[(h - 3) * 4] = 0xFF; and[(h - 3) * 4] = 0xFF;          // third row: mask set, color white => invert
        var dib = new byte[40 + palette.Length + xor.Length + and.Length];
        BinaryPrimitives.WriteInt32LittleEndian(dib, 40);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(4), w);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8), h * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(12), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(14), 1);
        palette.CopyTo(dib, 40);
        xor.CopyTo(dib, 48);
        and.CopyTo(dib, 48 + xor.Length);

        var img = Assert.Single(CurDecoder.Decode(WrapEntry(dib, w, h)).Images);
        var px = img.Bgra;
        Assert.Equal(new byte[] { 255, 255, 255, 255 }, px[..4].ToArray());
        Assert.Equal(0, px[w * 4 + 3]);
        Assert.Equal(new byte[] { 0, 0, 0, 200 }, px.Slice(2 * w * 4, 4).ToArray());
        Assert.True(img.HasInvertedPixels);
    }

    [Fact]
    public void Decodes_8bit_palette_cursor()
    {
        const int w = 4, h = 2;
        var palette = new byte[256 * 4];
        palette[5 * 4 + 1] = 128; // index 5 = green 128
        var xor = new byte[4 * h];
        xor[4] = 5; // top row (stored last), first pixel
        var dib = new byte[40 + palette.Length + xor.Length + 4 * h];
        BinaryPrimitives.WriteInt32LittleEndian(dib, 40);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(4), w);
        BinaryPrimitives.WriteInt32LittleEndian(dib.AsSpan(8), h * 2);
        BinaryPrimitives.WriteUInt16LittleEndian(dib.AsSpan(14), 8);
        palette.CopyTo(dib, 40);
        xor.CopyTo(dib, 40 + palette.Length);

        var img = Assert.Single(CurDecoder.Decode(WrapEntry(dib, w, h)).Images);
        Assert.Equal(new byte[] { 0, 128, 0, 255 }, img.Bgra[..4].ToArray());
    }

    [Theory]
    [InlineData(new byte[] { })]
    [InlineData(new byte[] { 0, 0, 3, 0, 1, 0 })]          // wrong type
    [InlineData(new byte[] { 0, 0, 2, 0, 0, 0 })]          // no images
    [InlineData(new byte[] { 0, 0, 2, 0, 1, 0, 32, 32 })] // directory cut short
    public void Rejects_files_that_are_not_cursors(byte[] data)
    {
        Assert.Throws<CursorFormatException>(() => CurDecoder.Decode(data));
    }

    [Fact]
    public void Rejects_entry_pointing_outside_the_file()
    {
        var bytes = Fixtures.Cur();
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), bytes.Length + 10);
        Assert.Throws<CursorFormatException>(() => CurDecoder.Decode(bytes));
    }

    [Fact]
    public void Rejects_truncated_pixel_data()
    {
        var bytes = Fixtures.Cur();
        var truncatedSize = 100;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), truncatedSize);
        Assert.Throws<CursorFormatException>(() => CurDecoder.Decode(bytes.AsSpan(0, 22 + truncatedSize)));
    }

    private static byte[] WrapEntry(byte[] dib, int w, int h)
    {
        var bytes = new byte[22 + dib.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(2), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(4), 1);
        bytes[6] = (byte)w;
        bytes[7] = (byte)h;
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(14), dib.Length);
        BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(18), 22);
        dib.CopyTo(bytes, 22);
        return bytes;
    }
}

public class AniDecoderTests
{
    [Fact]
    public void Reads_frames_timing_and_metadata()
    {
        var asset = CursorReader.Read(Fixtures.Ani(frames: 4, jiffies: 6));

        Assert.True(asset.IsAnimated);
        Assert.Equal(4, asset.Frames.Count);
        Assert.Equal(new[] { 0, 1, 2, 3 }, asset.Steps.Select(s => s.FrameIndex));
        Assert.All(asset.Steps, s => Assert.Equal(100, s.DurationMs)); // 6/60 s
        Assert.Equal("Spinner", asset.Title);
        Assert.Equal("Test Artist", asset.Artist);
        Assert.Equal(32, asset.LargestSize);
    }

    [Fact]
    public void Static_cursor_reads_as_single_step()
    {
        var asset = CursorReader.Read(Fixtures.Cur());
        Assert.False(asset.IsAnimated);
        Assert.Single(asset.Steps);
    }

    [Fact]
    public void Honors_seq_chunk()
    {
        var ani = AppendChunk(Fixtures.Ani(frames: 2), "seq ", [1, 0, 1]);
        var asset = AniDecoder.Decode(ani);
        Assert.Equal(new[] { 1, 0, 1 }, asset.Steps.Select(s => s.FrameIndex));
    }

    [Fact]
    public void Rejects_seq_pointing_to_missing_frame()
    {
        var ani = AppendChunk(Fixtures.Ani(frames: 2), "seq ", [0, 5]);
        Assert.Throws<CursorFormatException>(() => AniDecoder.Decode(ani));
    }

    [Fact]
    public void Rejects_riff_that_is_not_acon_or_has_no_frames()
    {
        var wave = "RIFF\x04\0\0\0WAVE"u8.ToArray();
        Assert.Throws<CursorFormatException>(() => AniDecoder.Decode(wave));
        var empty = "RIFF\x04\0\0\0ACON"u8.ToArray();
        Assert.Throws<CursorFormatException>(() => AniDecoder.Decode(empty));
    }

    [Fact]
    public void Rejects_chunk_larger_than_file()
    {
        var ani = Fixtures.Ani();
        Assert.Throws<CursorFormatException>(() => AniDecoder.Decode(ani.AsSpan(0, ani.Length - 50)));
    }

    private static byte[] AppendChunk(byte[] riff, string id, uint[] values)
    {
        var chunk = new byte[8 + values.Length * 4];
        System.Text.Encoding.ASCII.GetBytes(id).CopyTo(chunk, 0);
        BinaryPrimitives.WriteInt32LittleEndian(chunk.AsSpan(4), values.Length * 4);
        for (var i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteUInt32LittleEndian(chunk.AsSpan(8 + i * 4), values[i]);
        var result = riff.Concat(chunk).ToArray();
        BinaryPrimitives.WriteInt32LittleEndian(result.AsSpan(4), result.Length - 8);
        return result;
    }
}

public class PngWriterTests
{
    [Fact]
    public void Encodes_rgba_rows_that_decompress_back_to_the_pixels()
    {
        var bgra = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }; // 2x1
        var png = PngWriter.EncodeBgra(2, 1, bgra);

        Assert.True(PngWriter.TryReadSize(png, out var w, out var h));
        Assert.Equal((2, 1), (w, h));

        var idatStart = IndexOf(png, "IDAT"u8.ToArray());
        var idatLen = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(idatStart - 4));
        using var z = new ZLibStream(new MemoryStream(png, idatStart + 4, idatLen), CompressionMode.Decompress);
        using var raw = new MemoryStream();
        z.CopyTo(raw);
        Assert.Equal(new byte[] { 0, 3, 2, 1, 4, 7, 6, 5, 8 }, raw.ToArray());
    }

    private static int IndexOf(byte[] haystack, byte[] needle) => haystack.AsSpan().IndexOf(needle);
}
