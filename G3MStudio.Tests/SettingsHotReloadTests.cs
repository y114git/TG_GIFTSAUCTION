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
