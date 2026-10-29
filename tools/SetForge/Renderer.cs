using SkiaSharp;

namespace PointerParlor.SetForge;

/// <summary>Renders a glyph in a theme at a pixel size, returning straight-alpha BGRA pixels and the hotspot.</summary>
public static class Renderer
{
    public static (byte[] Bgra, int HotX, int HotY) Render(Glyph glyph, Theme theme, int size, int seed)
    {
        SKImage image;
        if (theme.Pixelate)
        {
            var low = Math.Max(16, size / 4);
            using var small = Draw(glyph, theme, low, seed, antialias: false);
            using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.DrawImage(small, new SKRect(0, 0, size, size), new SKSamplingOptions(SKFilterMode.Nearest));
            image = surface.Snapshot();
        }
        else
        {
            image = Draw(glyph, theme, size, seed, antialias: true);
        }

        using (image)
        {
            var info = new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Unpremul);
            using var bitmap = new SKBitmap(info);
            image.ReadPixels(info, bitmap.GetPixels(), bitmap.RowBytes, 0, 0);
            var scale = size / 32f;
            var hx = Math.Clamp((int)MathF.Round(glyph.Hotspot.X * scale), 0, size - 1);
            var hy = Math.Clamp((int)MathF.Round(glyph.Hotspot.Y * scale), 0, size - 1);
            return (bitmap.Bytes, hx, hy);
        }
    }

    private static SKImage Draw(Glyph glyph, Theme theme, int size, int seed, bool antialias)
    {
        using var surface = SKSurface.Create(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        var canvas = surface.Canvas;
        canvas.Clear(SKColors.Transparent);
        var s = size / 32f;
        canvas.Scale(s);
        if (theme.Scale != 1)
            canvas.Scale(theme.Scale, theme.Scale, glyph.Hotspot.X, glyph.Hotspot.Y);

        var ctx = new Ctx(theme, antialias, seed);
        if (theme.Splatter is { } colors)
            DrawSplatter(canvas, glyph, colors, seed, antialias);

        // Shadows go down first; each part then draws its own outline, so a badge's outline separates it from the arrow beneath.
        foreach (var part in glyph.Parts.Where(p => p.Kind is PartKind.Body or PartKind.Accent))
            DrawShadow(canvas, part, ctx);
        foreach (var part in glyph.Parts)
            DrawPart(canvas, part, ctx);
        return surface.Snapshot();
    }

    private sealed record Ctx(Theme Theme, bool Antialias, int Seed)
    {
        public SKPathEffect? Rough(int salt) =>
            Theme.Rough > 0 ? SKPathEffect.CreateDiscrete(1.6f, Theme.Rough, (uint)(Seed * 31 + salt)) : null;
    }

    private static void DrawShadow(SKCanvas canvas, Part part, Ctx ctx)
    {
        if (ctx.Theme.Shadow is not { } sh)
            return;
        using var paint = new SKPaint
        {
            IsAntialias = ctx.Antialias,
            Color = sh.Color,
            MaskFilter = sh.Blur > 0 ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, sh.Blur) : null,
        };
        canvas.Save();
        canvas.Translate(sh.Dx, sh.Dy);
        canvas.DrawPath(part.Path, paint);
        canvas.Restore();
    }

    private static void DrawPart(SKCanvas canvas, Part part, Ctx ctx)
    {
        var t = ctx.Theme;
        var line = t.Family == Family.Line;
        var semantic = t.SemanticBadges ? part.Color : null;
        switch (part.Kind)
        {
            case PartKind.Body:
                Strokes(canvas, part.Path, t.Under, ctx, 1);
                Fill(canvas, part.Path, ctx, t.FillGradient, t.Fill);
                Effects(canvas, part.Path, ctx);
                Strokes(canvas, part.Path, t.Over, ctx, 2);
                break;

            case PartKind.Accent when line:
            {
                var color = semantic ?? t.Accent;
                Strokes(canvas, part.Path, t.Under, ctx, 3);
                Fill(canvas, part.Path, ctx, null, semantic is null ? t.Fill : color.WithAlpha(0x40));
                Strokes(canvas, part.Path, [new StrokeLayer(color, t.LineWidth * 0.85f)], ctx, 4);
                break;
            }

            case PartKind.Accent:
                Strokes(canvas, part.Path, t.Under, ctx, 3);
                Fill(canvas, part.Path, ctx, null, t.Accent);
                Effects(canvas, part.Path, ctx, badge: true);
                break;

            case PartKind.Line:
                Strokes(canvas, part.Path, t.Under, ctx, 5, extra: t.LineWidth);
                Strokes(canvas, part.Path, t.Over.Length > 0 ? t.Over : [new StrokeLayer(t.Fill, t.LineWidth)], ctx, 6);
                break;

            case PartKind.AccentLine:
                Strokes(canvas, part.Path, t.Under, ctx, 7, extra: t.LineWidth);
                SpinnerArc(canvas, part.Path, ctx);
                break;

            case PartKind.Detail:
            {
                var color = part.Spinner ? t.Spinner[0] : semantic ?? (part.Color is { Alpha: < 0xFF } faint ? faint : t.Detail);
                if (t.DetailHalo is { } halo && !part.Spinner)
                    Strokes(canvas, part.Path, [new StrokeLayer(halo, 1.6f)], ctx, 8);
                using var paint = new SKPaint { IsAntialias = ctx.Antialias, Color = color };
                canvas.DrawPath(part.Path, paint);
                break;
            }
        }
    }

    private static void Fill(SKCanvas canvas, SKPath path, Ctx ctx, SKColor[]? gradient, SKColor color)
    {
        if (gradient is null && color.Alpha == 0)
            return;
        var bounds = path.Bounds;
        using var paint = new SKPaint
        {
            IsAntialias = ctx.Antialias,
            Color = color,
            Shader = gradient is null ? null : SKShader.CreateLinearGradient(
                new SKPoint(bounds.MidX, bounds.Top), new SKPoint(bounds.MidX, bounds.Bottom), gradient, SKShaderTileMode.Clamp),
        };
        canvas.DrawPath(path, paint);
    }

    /// <summary>Strokes a path with each layer; <paramref name="extra"/> widens layers for open lines so the halo clears the line.</summary>
    private static void Strokes(SKCanvas canvas, SKPath path, IEnumerable<StrokeLayer> layers, Ctx ctx, int salt, float extra = 0)
    {
        foreach (var layer in layers)
        {
            using var paint = new SKPaint
            {
                IsAntialias = ctx.Antialias,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = layer.Width + extra,
                StrokeJoin = SKStrokeJoin.Round,
                StrokeCap = SKStrokeCap.Round,
                Color = layer.Color,
                MaskFilter = layer.Blur > 0 ? SKMaskFilter.CreateBlur(SKBlurStyle.Normal, layer.Blur) : null,
                PathEffect = ctx.Rough(salt),
            };
            canvas.Save();
            canvas.Translate(layer.Dx, layer.Dy);
            canvas.DrawPath(path, paint);
            canvas.Restore();
        }
    }

    private static void SpinnerArc(SKCanvas canvas, SKPath arc, Ctx ctx)
    {
        var t = ctx.Theme;
        var b = arc.Bounds;
        var center = new SKPoint(b.MidX, b.MidY);
        using var paint = new SKPaint
        {
            IsAntialias = ctx.Antialias,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = t.LineWidth * 1.25f,
            StrokeCap = SKStrokeCap.Round,
            Color = t.Spinner[0],
            PathEffect = ctx.Rough(10),
        };
        if (t.Spinner.Length > 1)
        {
            // Sweep the gradient along the arc: it starts where the arc starts.
            var start = arc.GetPoint(0);
            var angle = MathF.Atan2(start.Y - center.Y, start.X - center.X) * 180 / MathF.PI;
            paint.Shader = SKShader.CreateSweepGradient(center, t.Spinner, null, SKShaderTileMode.Clamp, 0, 300,
                SKMatrix.CreateRotationDegrees(angle, center.X, center.Y));
        }
        canvas.DrawPath(arc, paint);
    }

    /// <summary>Gloss, scanlines and grid inside a shape. Badges get gloss only, so their small marks stay legible.</summary>
    private static void Effects(SKCanvas canvas, SKPath path, Ctx ctx, bool badge = false)
    {
        var t = ctx.Theme;
        if (!t.Gloss && (badge || (!t.Scanlines && !t.Grid)))
            return;
        canvas.Save();
        canvas.ClipPath(path, antialias: ctx.Antialias);
        var b = path.Bounds;
        if (t.Gloss)
        {
            using var gloss = new SKPaint
            {
                IsAntialias = ctx.Antialias,
                Shader = SKShader.CreateLinearGradient(new SKPoint(b.Left, b.Top), new SKPoint(b.Left, b.MidY),
                    [new SKColor(255, 255, 255, 110), new SKColor(255, 255, 255, 0)], SKShaderTileMode.Clamp),
            };
            canvas.DrawRect(new SKRect(b.Left, b.Top, b.Right, b.MidY), gloss);
        }
        if (t.Scanlines && !badge)
        {
            using var dark = new SKPaint { Color = new SKColor(0, 0, 0, 90), IsAntialias = false };
            for (var y = b.Top; y < b.Bottom; y += 1.5f)
                canvas.DrawRect(new SKRect(b.Left, y, b.Right, y + 0.5f), dark);
        }
        if (t.Grid && !badge)
        {
            using var grid = new SKPaint { Color = new SKColor(255, 255, 255, 70), StrokeWidth = 0.35f, Style = SKPaintStyle.Stroke, IsAntialias = ctx.Antialias };
            for (var x = 0f; x <= 32; x += 3.2f)
                canvas.DrawLine(x, 0, x, 32, grid);
            for (var y = 0f; y <= 32; y += 3.2f)
                canvas.DrawLine(0, y, 32, y, grid);
        }
        canvas.Restore();
    }

    /// <summary>A few paint drops and one drip behind the pointer, placed away from the hotspot.</summary>
    private static void DrawSplatter(SKCanvas canvas, Glyph glyph, SKColor[] colors, int seed, bool antialias)
    {
        var rnd = new Random(seed);
        var bounds = glyph.Parts[0].Path.Bounds;
        using var paint = new SKPaint { IsAntialias = antialias };
        for (var i = 0; i < 5; i++)
        {
            var x = bounds.Left + (float)rnd.NextDouble() * bounds.Width + 2;
            var y = bounds.Top + (float)rnd.NextDouble() * bounds.Height + 2;
            if (SKPoint.Distance(new SKPoint(x, y), glyph.Hotspot) < 7 || x > 31 || y > 31)
                continue;
            paint.Color = colors[i % colors.Length];
            var r = 0.6f + (float)rnd.NextDouble() * 1.0f;
            canvas.DrawCircle(x, y, r, paint);
            if (i == 0)
            {
                // One drip running down from the first drop.
                var len = 3 + (float)rnd.NextDouble() * 4;
                canvas.DrawRoundRect(new SKRect(x - r * 0.45f, y, x + r * 0.45f, Math.Min(31.5f, y + len)), r * 0.45f, r * 0.45f, paint);
                canvas.DrawCircle(x, Math.Min(31.5f, y + len), r * 0.6f, paint);
            }
        }
    }
}
