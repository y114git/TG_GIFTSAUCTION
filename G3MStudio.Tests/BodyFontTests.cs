using G3MStudio.Theme;

namespace G3MStudio.Tests;

public sealed class BodyFontTests
{
    [Theory]
    [InlineData("ja", "Yu Gothic UI")]
    [InlineData("ko", "Malgun Gothic")]
    [InlineData("zh_cn", "Microsoft YaHei UI")]
    [InlineData("zh_tw", "Microsoft JhengHei UI")]
    public void ACjkLanguageStartsItsFontStackWithItsOwnFace(string language, string face) =>
        Assert.StartsWith(face, BodyFont.For(language), StringComparison.Ordinal);

    [Theory]
    [InlineData("en")]
    [InlineData("ru")]
    [InlineData("es")]
    public void OtherLanguagesKeepTheThemeFont(string language) => Assert.Null(BodyFont.For(language));
}
