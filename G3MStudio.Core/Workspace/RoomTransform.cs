namespace G3MStudio.Core.Workspace;

/// <summary>Where an instance is and how it is scaled and turned. Rotation is in degrees, counter-clockwise as in GameMaker.</summary>
public readonly record struct Placement(double X, double Y, double ScaleX, double ScaleY, double Rotation);

public enum Handle { None, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left, Rotate }

/// <summary>The math behind the transform handles of the room canvas: scale about an anchor, rotate about a centre, and the factors a handle drag gives.</summary>
public static class RoomTransform
{
    private const double MinFactor = 0.01;

    public static Placement Scale(Placement p, double anchorX, double anchorY, double fx, double fy) =>
        p with { X = anchorX + (p.X - anchorX) * fx, Y = anchorY + (p.Y - anchorY) * fy, ScaleX = p.ScaleX * fx, ScaleY = p.ScaleY * fy };

    /// <summary>Turns the placement about a centre; the position orbits the centre and the rotation grows by the same angle.</summary>
    public static Placement Rotate(Placement p, double centerX, double centerY, double degrees)
    {
        var (sin, cos) = Math.SinCos(degrees * Math.PI / 180);
        double dx = p.X - centerX, dy = p.Y - centerY;
        return p with { X = centerX + dx * cos + dy * sin, Y = centerY - dx * sin + dy * cos, Rotation = Normalize(p.Rotation + degrees) };
    }

    /// <summary>The angle of the pointer around the centre in GameMaker's sense (counter-clockwise, 0 to the right).</summary>
    public static double AngleOf(double centerX, double centerY, double x, double y) => Math.Atan2(-(y - centerY), x - centerX) * 180 / Math.PI;

    public static double Normalize(double degrees) => (degrees % 360 + 360) % 360;

    public static double SnapAngle(double degrees, double step) => step > 0 ? Math.Round(degrees / step) * step : degrees;

    /// <summary>
    /// The scale factors and the fixed point for dragging a scale handle of <paramref name="start"/> to the pointer. A corner moves both axes, an edge one.
    /// Uniform keeps the ratio, from-centre keeps the centre of the box fixed instead of the opposite side.
    /// </summary>
    public static (double Fx, double Fy, double AnchorX, double AnchorY) Factors(RoomBox start, Handle handle, double px, double py, bool uniform, bool fromCenter)
    {
        var onLeft = handle is Handle.TopLeft or Handle.Left or Handle.BottomLeft;
        var onRight = handle is Handle.TopRight or Handle.Right or Handle.BottomRight;
        var onTop = handle is Handle.TopLeft or Handle.Top or Handle.TopRight;
        var onBottom = handle is Handle.BottomLeft or Handle.Bottom or Handle.BottomRight;
        var (ax, fx) = Axis(onLeft || onRight, onLeft ? start.Left : start.Right, onLeft ? start.Right : start.Left, start.CenterX, px, fromCenter);
        var (ay, fy) = Axis(onTop || onBottom, onTop ? start.Top : start.Bottom, onTop ? start.Bottom : start.Top, start.CenterY, py, fromCenter);
        if (uniform)
        {
            var movesX = onLeft || onRight;
            var movesY = onTop || onBottom;
            var driver = (movesX ? Math.Abs(fx) : 0) >= (movesY ? Math.Abs(fy) : 0) ? fx : fy;
            var size = Math.Abs(driver);
            (fx, fy) = (Math.CopySign(size, movesX ? fx : driver), Math.CopySign(size, movesY ? fy : driver));
        }

        return (fx, fy, ax, ay);
    }

    private static (double Anchor, double Factor) Axis(bool moves, double edge, double opposite, double center, double pointer, bool fromCenter)
    {
        if (!moves) return (center, 1);
        var anchor = fromCenter ? center : opposite;
        var length = edge - anchor;
        if (Math.Abs(length) < 1e-9) return (anchor, 1);
        var factor = (pointer - anchor) / length;
        return (anchor, Math.Abs(factor) < MinFactor ? Math.CopySign(MinFactor, factor == 0 ? 1 : factor) : factor);
    }
}
