namespace G3MStudio.Core.Settings;

/// <summary>
/// The one rule for animation: four levels, a duration multiplier per level, and what a level allows.
/// A chosen level of "" means the user never picked one, so the OS reduced-motion preference decides (Off) before the default (Medium).
/// </summary>
public static class MotionPolicy
{
    public const string Full = "Full";
    public const string Medium = "Medium";
    public const string Minimal = "Minimal";
    public const string Off = "Off";

    public static readonly string[] Levels = [Full, Medium, Minimal, Off];

    /// <summary>The longest any animation runs at Full, in milliseconds.</summary>
    public const int MaxMs = 200;

    /// <summary>The stored value when it is a known level, else "" (not chosen).</summary>
    public static string Normal(string? chosen) => chosen is not null && Levels.Contains(chosen) ? chosen : "";

    public static string Resolve(string? chosen, bool osReduced) => Normal(chosen) is { Length: > 0 } level ? level : osReduced ? Off : Medium;

    public static double Multiplier(string level) => level switch { Full => 1.0, Medium => 0.7, Minimal => 0.4, _ => 0.0 };

    /// <summary>Milliseconds for a base duration at a level; never above <see cref="MaxMs"/>, 0 when Off.</summary>
    public static int Milliseconds(string level, int baseMs) => (int)Math.Round(Math.Min(baseMs, MaxMs) * Multiplier(level));

    /// <summary>Slides and scales move things; Minimal keeps fades and colour changes only.</summary>
    public static bool AllowsMovement(string level) => level is Full or Medium;

    /// <summary>Work that ends sooner than this shows no progress bar; a bar that flashes up and goes is noise.</summary>
    public const int ProgressDelayMs = 300;

    /// <summary>True once <paramref name="elapsedMs"/> of work justifies showing progress.</summary>
    public static bool ShowsProgress(double elapsedMs) => elapsedMs >= ProgressDelayMs;

    /// <summary>Only a single new or changed item flashes: the flash says "here it is", which is lost when many rows light up (and costs a big list).</summary>
    public static bool FlashesItem(int itemCount) => itemCount == 1;

    /// <summary>The sideways offsets (pixels) of a rejected-input shake, fading out; the first and last are rest.</summary>
    public static readonly double[] ShakeOffsets = [0, -5, 5, -3, 3, 0];
}
