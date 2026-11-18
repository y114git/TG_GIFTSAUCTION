using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class SettingsHotReloadTests
{
    [Fact]
    public void AnEditFromOutsideIsAppliedAndItsOwnWriteIsNot()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var store = new SettingsStore(userData);
        var changes = 0;
        store.Changed += () => changes++;
        store.Update(x => x with { UiScale = 120 });
        Assert.Equal(1, changes);
        Assert.False(store.Reload()); // the file is what this program wrote

        File.WriteAllText(userData.Combine("settings.json"), /*lang=json,strict*/ "{ \"uiScale\": 150, \"colors\": { \"Bg.Window\": \"#101010\" } }");
        Assert.True(store.Reload());
        Assert.Equal(2, changes);
        Assert.Equal(150, store.Current.UiScale);
        Assert.Equal("#101010", store.Current.Colors["Bg.Window"]);
    }

    [Fact]
    public void AQuietUpdateIsSavedAndReadBackButRaisesNoChange()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var store = new SettingsStore(userData);
        var changes = 0;
        store.Changed += () => changes++;
        store.Update(x => x with { Audio = x.Audio with { Visualizer = false } }, notify: false);
        Assert.Equal(0, changes);
        Assert.False(store.Current.Audio.Visualizer);
        Assert.False(store.Reload()); // it is what this program wrote
        using var again = new SettingsStore(userData);
        Assert.False(again.Current.Audio.Visualizer);
    }

    [Fact]
    public void AFileThatDoesNotParseYetIsIgnoredAndTheNextGoodOneApplies()
    {
        using var dir = new TempDir();
        var userData = new UserData(dir.File("user"));
        using var store = new SettingsStore(userData);
        store.Update(x => x with { UiScale = 110 });
        File.WriteAllText(userData.Combine("settings.json"), "{ \"uiScale\": 1");
        Assert.False(store.Reload());
        Assert.Equal(110, store.Current.UiScale);
        File.WriteAllText(userData.Combine("settings.json"), /*lang=json,strict*/ "{ \"uiScale\": 130 }");
        Assert.True(store.Reload());
        Assert.Equal(130, store.Current.UiScale);
    }

    [Fact]
    public void EveryColorKeyHasATokenAndNoKeyIsListedTwice()
    {
        Assert.Equal(ThemeColors.Editable.Length, ThemeColors.Editable.Distinct().Count());
        foreach (var key in ThemeColors.Editable) Assert.NotEmpty(ThemeColors.Expand(key, "#112233"));
    }

    [Fact]
    public void HoverAndPressedFollowTheAccentUnlessTheUserChoseThem()
    {
        var followed = ThemeColors.FollowAccent(new Dictionary<string, string> { ["Accent"] = "#8855E0" }, "#8855E0");
        Assert.NotEqual("#2F6B50", followed["Hover"]);
        Assert.Equal(ThemeColors.ForDarkText("#8855E0"), followed["Selection.Bg"]); // lightened until the dark text reads at 4.5:1
        Assert.True(ThemeColors.Contrast(ThemeColors.DarkText, followed["Selection.Bg"]) >= 4.5);
        var own = ThemeColors.FollowAccent(new Dictionary<string, string> { ["Accent"] = "#8855E0", ["Hover"] = "#112233" }, "#8855E0");
        Assert.Equal("#112233", own["Hover"]);
        Assert.Equal(ThemeColors.Mix("#8855E0", "#3A3A3A", 0.82), own["Pressed"]); // only the colors the user chose stay
        Assert.Equal("#2F6D53", ThemeColors.FollowAccent(new Dictionary<string, string>(), "#039D5B")["Hover"]);
    }

    [Fact]
    public void SizesAreClampedUnknownKeysDropAndTheBodySizeMovesTheTypeScale()
    {
        var clamped = ThemeSizes.Clamp(new Dictionary<string, int> { ["Radius.Control"] = 99, ["Border.Width"] = 0, ["Nonsense"] = 5 });
        Assert.Equal(16, clamped["Radius.Control"]);
        Assert.Equal(1, clamped["Border.Width"]);
        Assert.DoesNotContain("Nonsense", clamped.Keys);
        var scale = ThemeSizes.Expand("FontSize.Body", 15).ToDictionary(p => p.Token, p => p.Value);
        Assert.Equal(13, scale["FontSize.Caption"]);
        Assert.Equal(22, scale["FontSize.Display"]);
        Assert.Equal(13, ThemeSizes.Value(new Dictionary<string, int>(), "FontSize.Body"));
    }

    [Fact]
    public void LightenMixesFortyFivePercentTowardsWhite()
    {
        Assert.Equal("#74C9A5", ThemeColors.Lighten("#039D5B"));
        Assert.Equal("#808080", ThemeColors.Mix("#000000", "#FFFFFF", 0.5));
    }
}
