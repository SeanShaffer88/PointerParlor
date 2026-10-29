using System.Buffers.Binary;
using System.Text;

namespace PointerParlor.Core.Imaging;

/// <summary>One step of an animation: which frame to show and for how long.</summary>
public readonly record struct AnimationStep(int FrameIndex, int DurationMs);

/// <summary>A static or animated cursor, ready for previews.</summary>
public sealed record CursorAsset(
    IReadOnlyList<CursorFile> Frames,
    IReadOnlyList<AnimationStep> Steps,
    string? Title,
    string? Artist)
{
    public bool IsAnimated => Frames.Count > 1;

    /// <summary>The largest image size every frame offers.</summary>
    public int LargestSize => Frames.Min(f => f.LargestSize);
}

/// <summary>Reads any cursor file: .ani (RIFF "ACON") or .cur/.ico.</summary>
public static class CursorReader
{
    public static CursorAsset Read(ReadOnlySpan<byte> data) =>
        data.Length >= 4 && data[..4].SequenceEqual("RIFF"u8)
            ? AniDecoder.Decode(data)
            : new CursorAsset([CurDecoder.Decode(data)], [new AnimationStep(0, 0)], null, null);

    public static CursorAsset ReadFile(string path) => Read(File.ReadAllBytes(path));
}

/// <summary>
/// Reads animated cursors: a RIFF "ACON" file with an "anih" header, optional "rate" and "seq "
/// chunks, and a LIST "fram" holding one complete .cur/.ico per frame.
/// </summary>
public static class AniDecoder
{
    private const int MaxFrames = 1024;

    public static CursorAsset Decode(ReadOnlySpan<byte> data)
    {
        if (data.Length < 12 || !data[..4].SequenceEqual("RIFF"u8) || !data.Slice(8, 4).SequenceEqual("ACON"u8))
            throw new CursorFormatException("The file is not an animated cursor (.ani).");

        var riffEnd = (int)Math.Min(data.Length, 8L + BinaryPrimitives.ReadUInt32LittleEndian(data[4..]));
        int frameCount = 0, stepCount = 0, defaultRate = 0;
        var haveHeader = false;
        uint[]? rates = null, sequence = null;
        var frames = new List<CursorFile>();
        string? title = null, artist = null;

        foreach (var (id, body) in Chunks(data[12..riffEnd]))
        {
            switch (id)
            {
                case "anih":
                    if (body.Length < 36)
                        throw new CursorFormatException("The animated cursor's header is cut short.");
                    frameCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(body[4..]);
                    stepCount = (int)BinaryPrimitives.ReadUInt32LittleEndian(body[8..]);
                    defaultRate = (int)BinaryPrimitives.ReadUInt32LittleEndian(body[28..]);
                    haveHeader = true;
                    break;
                case "rate":
                    rates = ReadUInts(body);
                    break;
                case "seq ":
                    sequence = ReadUInts(body);
                    break;
                case "LIST" when body.Length >= 4:
                    var listType = Encoding.ASCII.GetString(body[..4]);
                    foreach (var (subId, sub) in Chunks(body[4..]))
                    {
                        if (listType == "fram" && subId == "icon")
                        {
                            if (frames.Count >= MaxFrames)
                                throw new CursorFormatException("The animated cursor has too many frames.");
                            frames.Add(CurDecoder.Decode(sub));
                        }
                        else if (listType == "INFO" && subId == "INAM")
                        {
                            title = ReadText(sub);
                        }
                        else if (listType == "INFO" && subId == "IART")
                        {
                            artist = ReadText(sub);
                        }
                    }
                    break;
            }
        }

        if (!haveHeader)
            throw new CursorFormatException("The animated cursor has no header.");
        if (frames.Count == 0)
            throw new CursorFormatException("The animated cursor has no frames.");
        if (frameCount != frames.Count)
            frameCount = frames.Count;

        if (sequence is null)
        {
            stepCount = frameCount;
        }
        else
        {
            stepCount = sequence.Length;
            foreach (var index in sequence)
            {
                if (index >= frameCount)
                    throw new CursorFormatException("The animated cursor's sequence points to a missing frame.");
            }
        }
        if (stepCount == 0)
            throw new CursorFormatException("The animated cursor has no animation steps.");

        var steps = new AnimationStep[stepCount];
        for (var i = 0; i < stepCount; i++)
        {
            var frame = sequence is null ? i : (int)sequence[i];
            var jiffies = rates is not null && i < rates.Length ? (int)rates[i] : defaultRate;
            steps[i] = new AnimationStep(frame, Math.Max(1, jiffies) * 1000 / 60); // rates are in 1/60 s
        }
        return new CursorAsset(frames, steps, title, artist);
    }

    private static List<(string Id, byte[] Body)> Chunks(ReadOnlySpan<byte> data)
    {
        var list = new List<(string, byte[])>();
        var pos = 0;
        while (pos + 8 <= data.Length)
        {
            var id = Encoding.ASCII.GetString(data.Slice(pos, 4));
            var size = BinaryPrimitives.ReadUInt32LittleEndian(data[(pos + 4)..]);
            if (size > data.Length - pos - 8)
                throw new CursorFormatException($"The animated cursor's \"{id.Trim()}\" section is cut short.");
            list.Add((id, data.Slice(pos + 8, (int)size).ToArray()));
            pos += 8 + (int)size + (int)(size & 1); // chunks are padded to an even length
        }
        return list;
    }

    private static uint[] ReadUInts(ReadOnlySpan<byte> body)
    {
        var values = new uint[body.Length / 4];
        for (var i = 0; i < values.Length; i++)
            values[i] = BinaryPrimitives.ReadUInt32LittleEndian(body[(i * 4)..]);
        return values;
    }

    private static string? ReadText(ReadOnlySpan<byte> body)
    {
        var end = body.IndexOf((byte)0);
        var text = Encoding.Latin1.GetString(end >= 0 ? body[..end] : body).Trim();
        return text.Length == 0 ? null : text;
    }
}
