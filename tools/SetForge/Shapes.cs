using PointerParlor.Core;
using SkiaSharp;

namespace PointerParlor.SetForge;

public enum PartKind
{
    /// <summary>The main closed shape: themed fill and outline.</summary>
    Body,

    /// <summary>A closed badge or highlight shape in the accent color.</summary>
    Accent,

    /// <summary>Small marks drawn on top in the detail color (the "?" in a help badge).</summary>
    Detail,

    /// <summary>An open stroke drawn with the theme's line style (chevrons, brackets).</summary>
    Line,

    /// <summary>An open stroke in the accent color or spinner gradient.</summary>
    AccentLine,
}

/// <summary>One drawable piece of a cursor, in a 32×32 design grid. <see cref="Color"/> overrides the theme (semantic badges).</summary>
public sealed record Part(SKPath Path, PartKind Kind, SKColor? Color = null, bool Spinner = false);

/// <summary>A cursor design: parts in drawing order and the hotspot, in design units.</summary>
public sealed record Glyph(IReadOnlyList<Part> Parts, SKPoint Hotspot);

public enum Family
{
    /// <summary>Filled shapes with outlines.</summary>
    Solid,

    /// <summary>Rounded outlines, chevron resize cursors and colored badges.</summary>
    Line,
}

/// <summary>
/// The 17 pointer designs for both families. Shapes are plain geometry; themes decide how they look.
/// Animated roles take a phase from 0 to 1.
/// </summary>
public static class Shapes
{
    // Semantic badge colors used by Line-family themes.
    public static readonly SKColor HelpGold = new(0xF5, 0xB7, 0x00);
    public static readonly SKColor StopRed = new(0xE5, 0x48, 0x4D);
    public static readonly SKColor PlaceGreen = new(0x30, 0xA4, 0x6C);
    public static readonly SKColor PersonGray = new(0x8B, 0x8D, 0x98);

    private static readonly SKPoint Center = new(16, 16);
    private static readonly SKPoint BadgeCenter = new(22, 23);

    public static Glyph Build(CursorRole role, Family family, float phase = 0)
    {
        var line = family == Family.Line;
        return role switch
        {
            CursorRole.Arrow => new([Body(Arrow())], new(3, 3)),
            CursorRole.Help => WithBadge(HelpBadge(line)),
            CursorRole.AppStarting => new([Body(Arrow()), .. Spinner(BadgeCenter, 5.2f, phase, line, small: true)], new(3, 3)),
            CursorRole.Wait => new(Spinner(Center, 9.5f, phase, line, small: false).ToList(), Center),
            CursorRole.Crosshair => line ? new([Line(Segment(16, 4, 16, 28)), Line(Segment(4, 16, 28, 16))], Center) : new([Body(SolidCrosshair())], Center),
            CursorRole.IBeam => line ? new([Line(LineIBeam())], Center) : new([Body(SolidIBeam())], Center),
            CursorRole.NWPen => new([Body(Pencil()), Detail(Stroke(Segment(19.2f, 7.6f, 24.4f, 12.8f), 1.3f))], new(4, 28)),
            CursorRole.No => line
                ? new([Detail(Stroke(Circle(Center, 10), 2.8f).Op(Stroke(Segment(9, 9, 23, 23), 2.8f), SKPathOp.Union), StopRed)], Center)
                : new([Body(SolidNo())], Center),
            CursorRole.SizeNS => Resize(90, line),
            CursorRole.SizeWE => Resize(0, line),
            CursorRole.SizeNWSE => line ? new([Line(Poly(false, (6, 13), (6, 6), (13, 6))), Line(Poly(false, (26, 19), (26, 26), (19, 26)))], Center) : Resize(45, false),
            CursorRole.SizeNESW => line ? new([Line(Poly(false, (19, 6), (26, 6), (26, 13))), Line(Poly(false, (6, 19), (6, 26), (13, 26)))], Center) : Resize(-45, false),
            CursorRole.SizeAll => line ? new(MoveChevrons(), Center) : new([Body(SolidMove())], Center),
            CursorRole.UpArrow => line ? new([Body(Poly(true, (16, 3), (9, 21), (16, 18), (23, 21)))], new(16, 3)) : new([Body(SolidUpArrow())], new(16, 3)),
            CursorRole.Hand => new([Body(Hand()), .. HandCreases()], new(12.5f, 2.5f)),
            CursorRole.Pin => WithBadge(PinBadge(line)),
            CursorRole.Person => WithBadge(PersonBadge(line)),
            _ => throw new ArgumentOutOfRangeException(nameof(role)),
        };
    }

    // ---- parts ----

    private static Part Body(SKPath p) => new(p, PartKind.Body);
    private static Part Line(SKPath p) => new(p, PartKind.Line);
    private static Part Detail(SKPath p, SKColor? color = null) => new(p, PartKind.Detail, color);

    private static Glyph WithBadge(IEnumerable<Part> badge) => new([Body(Arrow()), .. badge], new(3, 3));

    private static IEnumerable<Part> HelpBadge(bool line)
    {
        var mark = new SKPath();
        mark.AddArc(new SKRect(19.6f, 18.8f, 24.4f, 23.6f), 200, 250);
        mark.LineTo(22, 24.6f);
        var question = Stroke(mark, 1.6f);
        question.AddCircle(22, 27.1f, 0.95f);
        return Badge(line, HelpGold, question);
    }

    private static IEnumerable<Part> PersonBadge(bool line)
    {
        var person = new SKPath();
        person.AddCircle(22, 20.9f, 1.9f);
        var shoulders = new SKPath();
        shoulders.AddArc(new SKRect(18.4f, 23.4f, 25.6f, 29.6f), 180, 180);
        shoulders.Close();
        return Badge(line, PersonGray, person.Op(shoulders, SKPathOp.Union));
    }

    private static IEnumerable<Part> Badge(bool line, SKColor semantic, SKPath mark)
    {
        var disc = Circle(BadgeCenter, 6.3f);
        if (line)
        {
            // Outline badge: tinted disc, colored ring, colored mark.
            yield return new Part(disc, PartKind.Accent, semantic);
            yield return Detail(mark, semantic);
        }
        else
        {
            yield return new Part(disc, PartKind.Accent);
            yield return Detail(mark);
        }
    }

    private static IEnumerable<Part> PinBadge(bool line)
    {
        var drop = Circle(new SKPoint(22, 21), 5.2f).Op(Poly(true, (17.5f, 23.4f), (26.5f, 23.4f), (22, 30)), SKPathOp.Union);
        var hole = Circle(new SKPoint(22, 21), 2);
        if (line)
        {
            yield return new Part(drop, PartKind.Accent, PlaceGreen);
            yield return Detail(Stroke(hole, 1.3f), PlaceGreen);
        }
        else
        {
            yield return new Part(drop, PartKind.Accent);
            yield return Detail(hole);
        }
    }

    private static IEnumerable<Part> Spinner(SKPoint c, float r, float phase, bool line, bool small)
    {
        var width = small ? 2.6f : 3.6f;
        if (line)
        {
            yield return new Part(Stroke(Circle(c, r), small ? 1.4f : 1.8f), PartKind.Detail, new SKColor(0, 0, 0, 0x30));
            var arc = new SKPath();
            arc.AddArc(new SKRect(c.X - r, c.Y - r, c.X + r, c.Y + r), phase * 360 - 90, 280);
            yield return new Part(arc, PartKind.AccentLine, Spinner: true);
        }
        else
        {
            var ring = Circle(c, r + width / 2).Op(Circle(c, r - width / 2), SKPathOp.Difference);
            yield return new Part(ring, small ? PartKind.Accent : PartKind.Body);
            var arc = new SKPath();
            arc.AddArc(new SKRect(c.X - r, c.Y - r, c.X + r, c.Y + r), phase * 360 - 90, 110);
            yield return new Part(Stroke(arc, width * 0.62f, SKStrokeCap.Round), PartKind.Detail, Spinner: true);
        }
    }

    private static IEnumerable<Part> HandCreases() =>
    [
        Detail(Stroke(Segment(15.2f, 12.2f, 15.2f, 16.2f), 0.9f)),
        Detail(Stroke(Segment(19.6f, 12.6f, 19.6f, 16.4f), 0.9f)),
        Detail(Stroke(Segment(24, 13.4f, 24, 16.8f), 0.9f)),
    ];

    private static Part[] MoveChevrons() =>
    [
        Line(Poly(false, (12, 8), (16, 4), (20, 8))),
        Line(Poly(false, (12, 24), (16, 28), (20, 24))),
        Line(Poly(false, (8, 12), (4, 16), (8, 20))),
        Line(Poly(false, (24, 12), (28, 16), (24, 20))),
    ];

    private static Glyph Resize(float degrees, bool line)
    {
        if (line)
        {
            var left = Poly(false, (9, 11), (5, 16), (9, 21));
            var right = Poly(false, (23, 11), (27, 16), (23, 21));
            return new([Line(Rotate(left, degrees)), Line(Rotate(right, degrees))], Center);
        }
        var shaft = Rect(9, 14.5f, 23, 17.5f);
        var arrows = shaft
            .Op(Poly(true, (2.5f, 16), (10.5f, 9.5f), (10.5f, 22.5f)), SKPathOp.Union)
            .Op(Poly(true, (29.5f, 16), (21.5f, 9.5f), (21.5f, 22.5f)), SKPathOp.Union);
        var scale = degrees % 90 == 0 ? 1f : 0.9f;
        return new([Body(Rotate(arrows, degrees, scale))], Center);
    }

    // ---- solid geometry ----

    private static SKPath Arrow() => Poly(true, (3, 3), (3, 24), (8, 19), (11.5f, 26.3f), (14.6f, 25), (11.2f, 17.8f), (18, 17.8f));

    private static SKPath SolidCrosshair()
    {
        var p = RoundRect(14.5f, 3, 17.5f, 12.5f, 1)
            .Op(RoundRect(14.5f, 19.5f, 17.5f, 29, 1), SKPathOp.Union)
            .Op(RoundRect(3, 14.5f, 12.5f, 17.5f, 1), SKPathOp.Union)
            .Op(RoundRect(19.5f, 14.5f, 29, 17.5f, 1), SKPathOp.Union);
        return p.Op(Circle(Center, 1.7f), SKPathOp.Union);
    }

    private static SKPath SolidIBeam() =>
        Rect(14.6f, 5, 17.4f, 27)
            .Op(RoundRect(10.5f, 3.4f, 21.5f, 6.6f, 1.4f), SKPathOp.Union)
            .Op(RoundRect(10.5f, 25.4f, 21.5f, 28.6f, 1.4f), SKPathOp.Union);

    private static SKPath LineIBeam()
    {
        var p = Segment(16, 6, 16, 26);
        p.AddPath(Segment(13.2f, 5, 18.8f, 5));
        p.AddPath(Segment(13.2f, 27, 18.8f, 27));
        return p;
    }

    private static SKPath SolidNo()
    {
        var ring = Circle(Center, 11).Op(Circle(Center, 7.2f), SKPathOp.Difference);
        var slash = Rotate(Rect(4, 14.2f, 28, 17.8f), 45).Op(Circle(Center, 10.5f), SKPathOp.Intersect);
        return ring.Op(slash, SKPathOp.Union);
    }

    private static SKPath SolidMove()
    {
        var ns = Rotate(Resize(0, false).Parts[0].Path, 90, 0.96f);
        return ns.Op(Rotate(Resize(0, false).Parts[0].Path, 0, 0.96f), SKPathOp.Union);
    }

    private static SKPath SolidUpArrow() =>
        Rect(13.6f, 10, 18.4f, 29).Op(Poly(true, (16, 3), (7.5f, 12.5f), (24.5f, 12.5f)), SKPathOp.Union);

    private static SKPath Pencil() =>
        Poly(true, (4, 28), (6.2f, 21.3f), (22.6f, 4.9f), (27.1f, 9.4f), (10.7f, 25.8f));

    private static SKPath Hand()
    {
        var p = RoundRect(10, 2.5f, 15, 18, 2.5f)
            .Op(RoundRect(14.6f, 10, 19.6f, 19, 2.3f), SKPathOp.Union)
            .Op(RoundRect(19.2f, 10.8f, 24, 19.5f, 2.3f), SKPathOp.Union)
            .Op(RoundRect(23.6f, 12, 28, 20, 2.1f), SKPathOp.Union)
            .Op(RoundRect(8.5f, 14.5f, 28, 29, 5), SKPathOp.Union);
        var thumb = Rotate(RoundRect(3.5f, 15.5f, 11.5f, 20.5f, 2.4f), 38, 1, new SKPoint(10, 18));
        return p.Op(thumb, SKPathOp.Union);
    }

    // ---- helpers ----

    public static SKPath Poly(bool closed, params (float X, float Y)[] points)
    {
        var p = new SKPath();
        p.MoveTo(points[0].X, points[0].Y);
        foreach (var (x, y) in points.Skip(1))
            p.LineTo(x, y);
        if (closed)
            p.Close();
        return p;
    }

    private static SKPath Segment(float x1, float y1, float x2, float y2) => Poly(false, (x1, y1), (x2, y2));

    private static SKPath Circle(SKPoint c, float r)
    {
        var p = new SKPath();
        p.AddCircle(c.X, c.Y, r);
        return p;
    }

    private static SKPath Rect(float l, float t, float r, float b)
    {
        var p = new SKPath();
        p.AddRect(new SKRect(l, t, r, b));
        return p;
    }

    private static SKPath RoundRect(float l, float t, float r, float b, float radius)
    {
        var p = new SKPath();
        p.AddRoundRect(new SKRect(l, t, r, b), radius, radius);
        return p;
    }

    private static SKPath Rotate(SKPath path, float degrees, float scale = 1, SKPoint? pivot = null)
    {
        var c = pivot ?? Center;
        var m = SKMatrix.CreateRotationDegrees(degrees, c.X, c.Y).PostConcat(SKMatrix.CreateScale(scale, scale, c.X, c.Y));
        var copy = new SKPath(path);
        copy.Transform(m);
        return copy;
    }

    /// <summary>Turns a stroke into a filled outline so it can be drawn and outlined like any shape.</summary>
    public static SKPath Stroke(SKPath path, float width, SKStrokeCap cap = SKStrokeCap.Round)
    {
        using var paint = new SKPaint { Style = SKPaintStyle.Stroke, StrokeWidth = width, StrokeCap = cap, StrokeJoin = SKStrokeJoin.Round };
        var result = new SKPath();
        paint.GetFillPath(path, result);
        return result;
    }
}
