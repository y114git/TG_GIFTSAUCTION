namespace G3MStudio.Core.Settings;

/// <summary>What the first-run scale is computed from: the primary screen in physical pixels, its OS scaling (1.0 = 100%) and optionally the window width in device-independent units (0 = unknown).</summary>
public readonly record struct ScreenInfo(int PixelWidth, int PixelHeight, double Scaling, double WindowWidth = 0);

/// <summary>The interface scale: automatic from the screen, or a percent the user chose; and the density presets.</summary>
public static class UiSizing
{
    public const int MinAuto = 80;
    public const int MaxScale = 200;
    public const int MinManual = 50;

    /// <summary>The scale the window aims for on this screen, percent. The OS scaling already makes a 1080p 100% screen and a 4K 150% screen alike,
    /// so the rest is the height in device-independent units against 1080, applied at half strength (a 4K screen at 100% gets 150%, a 1440p one 117%).</summary>
    public static int AutoScale(ScreenInfo screen)
    {
        if (screen.PixelHeight <= 0 || screen.Scaling <= 0) return 100;
        var ratio = screen.PixelHeight / screen.Scaling / 1080.0;
        var percent = 100 * (1 + (ratio - 1) * 0.5);
        // The window must keep room for the three docks: at least 960 units wide after the scale.
        if (screen.WindowWidth > 0) percent = Math.Min(percent, screen.WindowWidth / 960.0 * 100);
        return Math.Clamp((int)Math.Round(percent / 5) * 5, MinAuto, MaxScale);
    }

    /// <summary>The percent in force: the chosen one, or the automatic one when the setting is 0.</summary>
    public static int Effective(int uiScale, ScreenInfo screen) => uiScale == 0 ? AutoScale(screen) : Math.Clamp(uiScale, MinManual, MaxScale);

    public const string Compact = "Compact";
    public const string Comfortable = "Comfortable";
    public const string Spacious = "Spacious";

    public static readonly string[] Densities = [Compact, Comfortable, Spacious];

    public static string NormalDensity(string? density) => Densities.FirstOrDefault(d => d == density) ?? Comfortable;

    /// <summary>True when at least half of the window lies on the screens' work areas (they do not overlap), so a window mostly off screen is not restored. An unknown size counts as 300 by 200.</summary>
    public static bool MostlyVisible(int x, int y, int width, int height, IEnumerable<(int X, int Y, int Width, int Height)> areas)
    {
        (width, height) = (Math.Max(width, 300), Math.Max(height, 200));
        long visible = 0;
        foreach (var (ax, ay, aw, ah) in areas)
            visible += Math.Max(0L, Math.Min(x + width, ax + aw) - Math.Max(x, ax)) * Math.Max(0L, Math.Min(y + height, ay + ah) - Math.Max(y, ay));
        return visible * 2 >= (long)width * height;
    }

    /// <summary>The window rectangle pulled into the work area it overlaps most (the first area when it overlaps none): no wider or taller than the area, and moved inside it. A saved size from a larger screen must not push the right dock off this one.</summary>
    public static (int X, int Y, int Width, int Height) FitToArea(int x, int y, int width, int height, IReadOnlyList<(int X, int Y, int Width, int Height)> areas)
    {
        if (areas.Count == 0) return (x, y, width, height);
        var best = areas[0];
        long bestOverlap = -1;
        foreach (var area in areas)
        {
            var overlap = Math.Max(0L, Math.Min(x + width, area.X + area.Width) - Math.Max(x, area.X)) * Math.Max(0L, Math.Min(y + height, area.Y + area.Height) - Math.Max(y, area.Y));
            if (overlap > bestOverlap) (best, bestOverlap) = (area, overlap);
        }

        width = Math.Min(width, best.Width);
        height = Math.Min(height, best.Height);
        return (Math.Clamp(x, best.X, best.X + best.Width - width), Math.Clamp(y, best.Y, best.Y + best.Height - height), width, height);
    }

    /// <summary>The main window's size as a share of the work area, and the dock shares (Explorer, Inspector, bottom panel).</summary>
    public const double WindowShare = 0.85;
    public const double ExplorerShare = 0.19;
    public const double InspectorShare = 0.19;
    public const double BottomShare = 0.18;

    /// <summary>A window shorter than this (in layout px, after the interface scale) starts with the bottom dock folded; its panels open from the View menu.</summary>
    public const double FoldBottomBelow = 700;

    /// <summary>The window size in device-independent units for a work area given in physical pixels.</summary>
    public static (double Width, double Height) WindowSize(int workWidth, int workHeight, double scaling) =>
        (Math.Max(640, workWidth * WindowShare / scaling), Math.Max(480, workHeight * WindowShare / scaling));
}
