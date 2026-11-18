using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>A crosshair at the origin of a sprite, drawn over a frame that is shown at a whole-number scale.</summary>
public sealed class OriginMarker(GameMakerSprite sprite, double scale) : Control
{
    public override void Render(DrawingContext context)
    {
        var at = new Point((sprite.OriginX + 0.5) * scale, (sprite.OriginY + 0.5) * scale);
        if (!new Rect(Bounds.Size).Inflate(8).Contains(at)) return;
        foreach (var (pen, reach) in new[] { (new Pen(Brushes.Black, 3), 6.5), (new Pen(CanvasTheme.Handle, 1), 5.5) })
        {
            context.DrawLine(pen, at - new Vector(reach, 0), at + new Vector(reach, 0));
            context.DrawLine(pen, at - new Vector(0, reach), at + new Vector(0, reach));
        }
    }
}
