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

    /// <summary>The main window's size as a share of the work area, and the dock shares (Explorer, Inspector, bottom panel).</summary>
    public const double WindowShare = 0.85;
    public const double ExplorerShare = 0.22;
    public const double InspectorShare = 0.22;
    public const double BottomShare = 0.18;

    /// <summary>The window size in device-independent units for a work area given in physical pixels.</summary>
    public static (double Width, double Height) WindowSize(int workWidth, int workHeight, double scaling) =>
        (Math.Max(640, workWidth * WindowShare / scaling), Math.Max(480, workHeight * WindowShare / scaling));
}
