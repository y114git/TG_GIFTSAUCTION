using G3MStudio.Core.Settings;

namespace G3MStudio.Tests;

public sealed class CrashScrubTests
{
    [Fact]
    public void TheHomeFolderBecomesATildeAndOtherPathsAreHashed()
    {
        var text = CrashScrub.Text(@"Cannot open C:\Users\Ann\Games\Undertale\data.win and D:\mods\secret.zip", @"C:\Users\Ann");
        Assert.DoesNotContain("Zoe", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Undertale", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".win>", text, StringComparison.Ordinal);
        Assert.Contains(".zip>", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TheSamePathGivesTheSameHashAndPlainTextIsKept()
    {
        Assert.Equal(CrashScrub.Text("/home/zoe/a.win", "/nowhere"), CrashScrub.Text("/home/zoe/a.win", "/nowhere"));
        Assert.Equal("Index was out of range", CrashScrub.Text("Index was out of range", "/home/zoe"));
        Assert.Equal("", CrashScrub.Text(null));
    }
}

public sealed class CrashReportingTests
{
    [Fact]
    public void ABuildWithoutAKeyStaysSilentWhateverTheSetting()
    {
        Assert.Null(G3MStudio.CrashReporting.Dsn);
        G3MStudio.CrashReporting.Apply(true);
        G3MStudio.CrashReporting.Apply(false);
    }

    [Theory]
    [InlineData("not a dsn")]
    [InlineData("https://abc@example.invalid/1")]
    [InlineData("")]
    public void AKeyThatIsWrongOrUnreachableNeverBreaksTheApp(string key)
    {
        G3MStudio.CrashReporting.Apply(true, key);
        G3MStudio.CrashReporting.Apply(true, key);
        G3MStudio.CrashReporting.Apply(false);
    }

    [Fact]
    public void ReportsAreOnByDefaultAndTheSwitchIsKept()
    {
        Assert.True(new AppSettings().CrashReports);
        Assert.False((new AppSettings() with { CrashReports = false }).CrashReports);
    }
}
