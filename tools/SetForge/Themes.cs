using SkiaSharp;

namespace PointerParlor.SetForge;

/// <summary>A stroke drawn along a shape: under the fill it reads as an outline or glow, over it as a line.</summary>
public sealed record StrokeLayer(SKColor Color, float Width, float Blur = 0, float Dx = 0, float Dy = 0);

public sealed record Shadow(SKColor Color, float Dx, float Dy, float Blur);

/// <summary>How a set looks. All lengths are in the 32-unit design grid.</summary>
public sealed record Theme
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public Family Family { get; init; } = Family.Solid;

    public SKColor Fill { get; init; } = SKColors.White;

    /// <summary>Top-to-bottom gradient for filled shapes; overrides <see cref="Fill"/>.</summary>
    public SKColor[]? FillGradient { get; init; }

    public StrokeLayer[] Under { get; init; } = [];
    public StrokeLayer[] Over { get; init; } = [];

    public SKColor Accent { get; init; } = SKColors.Black;
    public SKColor Detail { get; init; } = SKColors.White;
    public SKColor? DetailHalo { get; init; }
    public SKColor[] Spinner { get; init; } = [SKColors.White];

    /// <summary>Line-family themes: use gold/red/green/gray badges instead of the accent.</summary>
    public bool SemanticBadges { get; init; }

    /// <summary>Width of open strokes (chevrons, spinner arc) in Line-family themes.</summary>
    public float LineWidth { get; init; } = 1.8f;

    public Shadow? Shadow { get; init; }
    public float Rough { get; init; }
    public bool Gloss { get; init; }
    public bool Scanlines { get; init; }
    public bool Grid { get; init; }
    public bool Pixelate { get; init; }
    public SKColor[]? Splatter { get; init; }

    /// <summary>Draws the pointer larger around its hotspot (Beacon XL).</summary>
    public float Scale { get; init; } = 1;

    private static SKColor C(uint argb) => new(argb);

    public static IReadOnlyList<Theme> All { get; } =
    [
        new()
        {
            Id = "velvet", Name = "Velvet",
            Description = "Plum velvet with a gold trim: PointerParlor's house style.",
            FillGradient = [C(0xFF8A2F5B), C(0xFF4A1230)],
            Under = [new(C(0xFF2C0A1C), 4.6f), new(C(0xFFE8B64C), 3.0f)],
            Accent = C(0xFFE8B64C), Detail = C(0xFF3A0E24), Spinner = [C(0xFFF6D27A)],
            Shadow = new(C(0x66000000), 0.6f, 1.0f, 0.8f), Gloss = true,
        },
        new()
        {
            Id = "lagoon", Name = "Lagoon",
            Description = "Crisp white pointers outlined in deep teal, with coral and gold, like the app icon.",
            Fill = SKColors.White,
            Under = [new(C(0xFF0F2A33), 3.2f)],
            Accent = C(0xFFFF7A59), Detail = SKColors.White, Spinner = [C(0xFFF5C04A)],
            Shadow = new(C(0x55000000), 0.5f, 0.9f, 0.7f),
        },
        new()
        {
            Id = "brass", Name = "Brass",
            Description = "Polished brass with a dark bevel, like fittings in an old parlor.",
            FillGradient = [C(0xFFFBE3A0), C(0xFFC8903A), C(0xFFF0C766)],
            Under = [new(C(0xFF3B2408), 3.0f)],
            Accent = C(0xFFB4552D), Detail = C(0xFFFBE3A0), Spinner = [C(0xFF6B3F12)],
            Shadow = new(C(0x66000000), 0.6f, 1.0f, 0.8f), Gloss = true,
        },
        new()
        {
            Id = "ink", Name = "Ink",
            Description = "Rough black ink with crimson, cyan and orange paint splatter.",
            Fill = C(0xFF111214),
            Under = [new(SKColors.White, 3.0f)],
            Accent = C(0xFFD81B4A), Detail = SKColors.White, Spinner = [C(0xFF22C1D6)],
            Rough = 0.35f, Splatter = [C(0xFFD81B4A), C(0xFF22C1D6), C(0xFFFF8A1E)],
        },
        new()
        {
            Id = "chalk", Name = "Chalk",
            Description = "Hand-drawn chalk lines on a slate backing.",
            Family = Family.Line, SemanticBadges = true,
            Fill = C(0x8C2B3A3A),
            Under = [new(C(0xFF2B3A3A), 4.4f)],
            Over = [new(C(0xFFF4F1E8), 2.0f)],
            Accent = C(0xFFF4F1E8), Detail = C(0xFFF4F1E8), Spinner = [C(0xFFF4F1E8), C(0xFF9AD8E2)],
            Rough = 0.45f, LineWidth = 2.0f,
        },
        new()
        {
            Id = "pixel", Name = "Pixel",
            Description = "Chunky retro pixels in black and white.",
            Fill = SKColors.White,
            Under = [new(SKColors.Black, 2.8f)],
            Accent = C(0xFF3B5BDB), Detail = SKColors.White, Spinner = [C(0xFF3B5BDB)],
            Pixelate = true,
        },
        new()
        {
            Id = "glass", Name = "Glass",
            Description = "Thin rounded outlines over frosted glass, with colored badges and a gradient spinner.",
            Family = Family.Line, SemanticBadges = true,
            Fill = C(0x8CFFFFFF),
            Under = [new(C(0xE6FFFFFF), 4.2f, 0.5f)],
            Over = [new(C(0xFF1B1B1F), 1.7f)],
            Accent = C(0xFF1B1B1F), Detail = C(0xFF1B1B1F), DetailHalo = C(0xE6FFFFFF),
            Spinner = [C(0xFFB86FA8), C(0xFFF08A4B), C(0xFF9AA5A8)],
            LineWidth = 1.8f,
        },
        new()
        {
            Id = "candy", Name = "Candy",
            Description = "Glossy pastel pink and lavender with a white candy shell.",
            FillGradient = [C(0xFFFF9EC7), C(0xFFB79CFF)],
            Under = [new(C(0xFF6B3D7A), 4.6f), new(SKColors.White, 3.0f)],
            Accent = C(0xFF7FE0C8), Detail = C(0xFF6B3D7A), Spinner = [SKColors.White],
            Shadow = new(C(0x40000000), 0.5f, 0.9f, 0.8f), Gloss = true,
        },
        new()
        {
            Id = "blueprint", Name = "Blueprint",
            Description = "White technical linework on blueprint blue, with a drafting grid.",
            Fill = C(0xFF1F4E8C),
            Under = [new(C(0xFF0B2447), 4.2f), new(SKColors.White, 2.2f)],
            Accent = SKColors.White, Detail = C(0xFF1F4E8C), Spinner = [SKColors.White],
            Grid = true,
        },
        new()
        {
            Id = "phosphor", Name = "Phosphor",
            Description = "Glowing green on black, like an old terminal screen.",
            Fill = C(0xFF03150A),
            Under = [new(C(0x9939FF6A), 5.5f, 1.4f), new(C(0xFF39FF6A), 2.2f)],
            Accent = C(0xFF39FF6A), Detail = C(0xFF03150A), Spinner = [C(0xFFB6FFC8)],
            Scanlines = true,
        },
        new()
        {
            Id = "neon", Name = "Neon",
            Description = "Glitching neon outlines in pink and cyan, with orange sparks.",
            Family = Family.Line,
            Fill = C(0xCC14081F),
            Under = [new(C(0xB3FF2BD6), 5.0f, 1.6f), new(C(0xFF0A0410), 3.6f)],
            Over = [new(C(0xFF22E6FF), 1.5f, 0, -0.6f), new(C(0xFFFF2BD6), 1.5f, 0, 0.6f), new(C(0xFFFFFFFF), 0.9f)],
            Accent = C(0xFFFF8A1E), Detail = C(0xFFFF8A1E), Spinner = [C(0xFFFF8A1E), C(0xFFFF2BD6), C(0xFF22E6FF)],
            LineWidth = 1.6f,
        },
        new()
        {
            Id = "beacon-xl", Name = "Beacon XL",
            Description = "Extra large, high-contrast yellow and black, easy to spot on any screen.",
            Fill = C(0xFFFFD400),
            Under = [new(SKColors.White, 5.6f), new(SKColors.Black, 3.6f)],
            Accent = SKColors.Black, Detail = C(0xFFFFD400), Spinner = [SKColors.Black],
            Scale = 1.12f,
        },
    ];
}
