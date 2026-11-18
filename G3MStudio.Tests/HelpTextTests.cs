using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Settings;
using G3MStudio.Editor;
using G3MStudio.Theme;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>The explanatory text setting: hints and details follow the level at once, the owner's own visibility stays honoured, and an unknown level falls back to All.</summary>
public sealed class HelpTextTests
{
    [Theory]
    [InlineData(HelpLevel.All, true, true)]
    [InlineData(HelpLevel.Hints, true, false)]
    [InlineData(HelpLevel.Details, false, true)]
    [InlineData(HelpLevel.Off, false, false)]
    public void ALevelShowsItsKindsOfText(HelpLevel level, bool hint, bool detail)
    {
        Assert.Equal(hint, HelpText.Allows(level, HelpKind.Hint));
        Assert.Equal(detail, HelpText.Allows(level, HelpKind.Detail));
        Assert.True(HelpText.Allows(level, HelpKind.None)); // an error or a question is never hidden
    }

    [Fact]
    public Task ATaggedBlockFollowsTheLevelAndKeepsTheWishOfItsOwner() => HeadlessApp.Run(() =>
    {
        var hint = new TextBlock { Text = "how" }.Hint();
        var detail = new TextBlock { Text = "what" }.Detail();
        var plain = new TextBlock { Text = "error" };
        var wished = new TextBlock { Text = "later", IsVisible = false }.Hint();
        try
        {
            Assert.True(hint.IsVisible && detail.IsVisible && !wished.IsVisible);
            HelpText.Level = HelpLevel.Details;
            Assert.False(hint.IsVisible);
            Assert.True(detail.IsVisible && plain.IsVisible);
            wished.IsVisible = true; // the owner wants it, the level does not
            Assert.False(wished.IsVisible);
            HelpText.Level = HelpLevel.Off;
            Assert.False(detail.IsVisible);
            Assert.True(plain.IsVisible);
            HelpText.Level = HelpLevel.Hints;
            Assert.True(hint.IsVisible && wished.IsVisible && !detail.IsVisible);
            wished.IsVisible = false;
            HelpText.Level = HelpLevel.All;
            Assert.True(hint.IsVisible && detail.IsVisible);
            Assert.False(wished.IsVisible);
            HelpText.SetKind(hint, HelpKind.None);
            HelpText.Level = HelpLevel.Off;
            Assert.True(hint.IsVisible); // untagged: shown whatever the level
        }
        finally { HelpText.Level = HelpLevel.All; }

        return Task.CompletedTask;
    });

    [Fact]
    public Task ABlockHiddenByTheLevelKeepsItsWishWhenItsKindChanges() => HeadlessApp.Run(() =>
    {
        var block = new TextBlock { Text = "how" }.Hint();
        try
        {
            HelpText.Level = HelpLevel.Details;
            Assert.False(block.IsVisible);
            block.Detail();
            Assert.True(block.IsVisible);
        }
        finally { HelpText.Level = HelpLevel.All; }

        return Task.CompletedTask;
    });

    [Fact]
    public Task AnOwnerHidingABlockTheLevelHidesAlreadyKeepsItHiddenAfterwards() => HeadlessApp.Run(() =>
    {
        var detail = new TextBlock { Text = "what" }.Detail();
        try
        {
            HelpText.Level = HelpLevel.Hints;
            Assert.False(detail.IsVisible);
            detail.IsVisible = false; // the owner no longer wants it; the value it sees does not change
            HelpText.Level = HelpLevel.All;
            Assert.False(detail.IsVisible);
            detail.IsVisible = true;
            Assert.True(detail.IsVisible);
        }
        finally { HelpText.Level = HelpLevel.All; }

        return Task.CompletedTask;
    });

    [Fact]
    public async Task TheReasonASoundHasNoAudioShowsAtEveryLevel()
    {
        await HeadlessApp.Run(() =>
        {
            var sound = new G3MLib.DataFile.Models.GameMakerSound { Name = new G3MLib.DataFile.Models.GameMakerString("snd"), File = new G3MLib.DataFile.Models.GameMakerString("missing.ogg") };
            var view = new SoundPlayerView(sound, _ => true);
            var status = AudioPlaybackTests.Find<TextBlock>(view, "SoundStatus");
            Assert.Contains("missing.ogg", status.Text, StringComparison.Ordinal);
            Assert.Equal(HelpKind.None, HelpText.GetKind(status)); // a reason, not explanatory text the setting may hide
            return Task.CompletedTask;
        });
    }

    [Fact]
    public void AnUnknownLevelInTheSettingsFileBecomesAll()
    {
        Assert.Equal(HelpLevel.All, (new AppSettings { HelpLevel = (HelpLevel)42 }).Clamped().HelpLevel);
        Assert.Equal(HelpLevel.Details, (new AppSettings { HelpLevel = HelpLevel.Details }).Clamped().HelpLevel);
    }

    [Fact]
    public async Task TheSoundEditorTextFollowsTheSettingLiveFromTheSettingsPage()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var view = new SoundPlayerView(AudioPlaybackTests.Sound(2), _ => true, () => new G3MStudio.Core.Settings.AudioOptions());
                var host = new Window { Content = view, Width = 700, Height = 600 };
                host.Show();
                await LayoutAudit.Settle();
                var region = AudioPlaybackTests.Find<TextBlock>(view, "AudioRegionHint");
                var status = AudioPlaybackTests.Find<TextBlock>(view, "SoundStatus");
                Assert.True(region.IsVisible && status.IsVisible);

                window.ShowSettings();
                await LayoutAudit.Settle();
                var combo = window.OpenSettings!.GetVisualDescendants().OfType<ComboBox>().Single(c => c.Name == "HelpText");
                Assert.Equal(4, combo.ItemCount);
                Assert.Equal(0, combo.SelectedIndex); // All is the default

                combo.SelectedIndex = (int)HelpLevel.Details;
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(HelpLevel.Details, window.Workspaces.Settings.Current.HelpLevel);
                Assert.False(region.IsVisible);
                Assert.True(status.IsVisible);

                combo.SelectedIndex = (int)HelpLevel.Hints;
                Dispatcher.UIThread.RunJobs();
                Assert.True(region.IsVisible);
                Assert.False(status.IsVisible);

                combo.SelectedIndex = (int)HelpLevel.Off;
                Dispatcher.UIThread.RunJobs();
                Assert.False(region.IsVisible || status.IsVisible);

                window.Workspaces.Settings.Update(x => x with { HelpLevel = HelpLevel.All });
                Dispatcher.UIThread.RunJobs();
                Assert.True(region.IsVisible && status.IsVisible);
                host.Close();
            }
            finally
            {
                HelpText.Level = HelpLevel.All;
                window.Close();
            }
        });
    }
}
