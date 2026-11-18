namespace G3MStudio.Theme;

/// <summary>
/// The body font of a language. The default stack lists Latin faces first, so a menu text that starts with a kana or a Han character was
/// drawn in whichever CJK face matched it, and one menu mixed two faces. A CJK language puts its own UI face first, so every text uses one.
/// </summary>
public static class BodyFont
{
    /// <summary>The font stack for a language, or null when the theme's own <c>Font.Body</c> fits.</summary>
    public static string? For(string language) => language switch
    {
        "ja" => "Yu Gothic UI, Segoe UI, Arial, sans-serif",
        "ko" => "Malgun Gothic, Segoe UI, Arial, sans-serif",
        "zh_cn" => "Microsoft YaHei UI, Segoe UI, Arial, sans-serif",
        "zh_tw" => "Microsoft JhengHei UI, Segoe UI, Arial, sans-serif",
        _ => null,
    };
}
