using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MLib.DataFile.Models;
using G3MStudio.Audio;
using G3MStudio.Core.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class AudioPlaybackTests
{
    private const int Rate = 8000;

    private static PcmAudio Clip(double seconds) => new([.. Enumerable.Range(0, (int)(seconds * Rate)).Select(i => i / (float)(seconds * Rate))], 1, Rate);

    private static byte[] Wav(double seconds) => AudioCodec.Encode(Clip(seconds), "wav");

    private static GameMakerSound Sound(double seconds) => new() { Name = new GameMakerString("snd"), AudioFile = new GameMakerEmbeddedAudio { Data = Wav(seconds) }, AudioLength = (float)seconds };

    private static T Find<T>(Control root, string name) where T : Control =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<T>().Single(c => c.Name == name);

    private static NullAudioOutput UseNullDevice()
    {
        var output = new NullAudioOutput();
        AudioDevices.Shutdown();
        AudioDevices.Open = (_, _) => output;
        return output;
    }

    [Fact]
    public void TheFeederCutsARangeLoopsItAndWrapsPositions()
    {
        var feeder = new PcmFeeder(Clip(1));
        var block = new short[400];
        feeder.Begin(0.25, 0.5); // frames 2000 to 4000
        var (frames, from) = feeder.Read(block, 300);
        Assert.Equal((300, 2000L), (frames, from));
        Assert.Equal(2300, feeder.Next);
        Assert.Equal((1700, 2300L), feeder.Read(new short[3000], 3000)); // the rest of the range
        Assert.Equal(0, feeder.Read(block, 300).Frames); // no loop: over

        feeder.Loop = true;
        feeder.Begin(0.25, 0.5);
        var (wrapped, _) = feeder.Read(new short[2 * 2500], 2500); // 500 frames past the end come from the start again
        Assert.Equal(2500, wrapped);
        Assert.Equal(2500, feeder.Next);
        Assert.Equal(2000 + 500, feeder.Wrap(4500));
        Assert.Equal(3999, feeder.Wrap(3999));

        feeder.Begin(0.5, 0.25); // an end before the start means the end of the clip
        Assert.Equal(4000, feeder.Next);
        feeder.Loop = false;
        Assert.Equal(4000, feeder.Read(new short[5000], 5000).Frames);
    }

    [Fact]
    public void TheFeederMixesMoreChannelsDownToTwoAndClipsToSixteenBits()
    {
        var feeder = new PcmFeeder(new PcmAudio([1f, -1f, 0.5f, 2f, -2f, 0f], 3, Rate));
        feeder.Begin(0, 0);
        var block = new short[4];
        Assert.Equal(2, feeder.Channels);
        Assert.Equal(2, feeder.Read(block, 2).Frames);
        Assert.Equal([short.MaxValue, -32767, short.MaxValue, short.MinValue], block); // the third channel is dropped
    }

    [Fact]
    public void TheNullOutputKeepsStateAndAClockOnlyAdvanceMoves()
    {
        using var output = new NullAudioOutput { Loop = false };
        var ended = 0;
        output.Ended += () => ended++;
        output.Load(Clip(2));
        output.Play(0.5, 1.0);
        Assert.True(output.IsPlaying);
        output.Advance(0.25);
        Assert.Equal(0.75, output.Position, 3);
        output.Pause();
        output.Advance(1);
        Assert.Equal(0.75, output.Position, 3);
        output.Resume();
        output.Loop = true;
        output.Advance(0.5); // 1.25 wraps to 0.75
        Assert.Equal(0.75, output.Position, 3);
        output.Loop = false;
        output.Advance(0.5);
        Assert.False(output.IsActive);
        Assert.Equal(1, ended);
    }

    [Fact]
    public async Task TheSoundViewPlaysPausesSeeksLoopsAndPreviewsARegionOnTheDevice()
    {
        await HeadlessApp.Run(async () =>
        {
            var output = UseNullDevice();
            var view = new SoundPlayerView(Sound(2), _ => throw new InvalidOperationException("the system player must not open"), () => new AudioOptions { Volume = 40 });
            var window = new Window { Width = 600, Height = 500, Content = view };
            window.Show();
            try
            {
                var transport = view.Transport ?? throw new InvalidOperationException("no player");
                Assert.Contains("0:02.0", transport.TimeText, StringComparison.Ordinal);

                await transport.PlayPauseAsync();
                Assert.True(transport.IsPlaying);
                Assert.Equal(0.4, output.Volume, 3); // the start volume of the Sound settings
                output.Advance(0.5);
                transport.Tick();
                Assert.Equal(0.5, transport.Position, 3);
                Assert.Contains("0:00.5", transport.TimeText, StringComparison.Ordinal);

                await transport.PlayPauseAsync();
                Assert.False(transport.IsPlaying);
                Assert.True(output.IsActive);
                output.Advance(1);
                Assert.Equal(0.5, output.Position, 3);
                await transport.PlayPauseAsync();
                transport.SeekTo(1.5);
                Assert.Equal(1.5, output.Position, 3);

                Find<CheckBox>(view, "AudioLoop").IsChecked = true;
                Assert.True(output.Loop);
                output.Advance(1); // 2.5 wraps to 0.5
                Assert.Equal(0.5, output.Position, 3);
                Find<CheckBox>(view, "AudioLoop").IsChecked = false;

                Find<NumericUpDown>(view, "SoundRegionStart").Value = 1.0m;
                Find<NumericUpDown>(view, "SoundRegionEnd").Value = 1.5m;
                await transport.PlayRegionAsync();
                Assert.Equal(1.0, output.Position, 3);
                output.Advance(0.6); // past the region end without a loop
                transport.Tick();
                Assert.False(transport.IsPlaying);
                Assert.Equal(0, transport.Position, 3);

                Find<CheckBox>(view, "AudioLoop").IsChecked = true;
                await transport.PlayRegionAsync();
                output.Advance(0.6); // 1.6 wraps to 1.1
                Assert.Equal(1.1, output.Position, 3);

                view.StopPlayback(); // the tab closes
                Assert.False(output.IsActive);
            }
            finally
            {
                window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task AnotherPlayerTakingTheDeviceStopsThePlayerBefore()
    {
        await HeadlessApp.Run(async () =>
        {
            var output = UseNullDevice();
            var first = new SoundPlayerView(Sound(2), _ => true).Transport!;
            var second = new SoundPlayerView(Sound(1), _ => true).Transport!;
            try
            {
                await first.PlayPauseAsync();
                Assert.True(first.IsPlaying);
                await second.PlayPauseAsync();
                Dispatcher.UIThread.RunJobs();
                Assert.True(second.IsPlaying);
                Assert.False(first.IsPlaying);
                Assert.True(output.IsPlaying);
            }
            finally { AudioDevices.Shutdown(); }
        });
    }

    [Fact]
    public async Task ChangingTheDeviceTellsTheOwnerBeforeTheOutputIsClosed()
    {
        await HeadlessApp.Run(() =>
        {
            UseNullDevice();
            var told = 0;
            try
            {
                Assert.NotNull(AudioDevices.Acquire(new AudioOptions(), new object(), () => told++));
                Assert.NotNull(AudioDevices.Acquire(new AudioOptions { BufferMs = 123 }, new object(), () => { }));
                Assert.Equal(1, told);
            }
            finally { AudioDevices.Shutdown(); }
        });
    }

    [Fact]
    public async Task TheBackendOptionPicksOpenAlTheSystemPlayerOrNothing()
    {
        await HeadlessApp.Run(() =>
        {
            var opens = 0;
            AudioDevices.Shutdown();
            AudioDevices.Open = (_, _) =>
            {
                opens++;
                return new NullAudioOutput();
            };
            var owner = new object();
            Assert.NotNull(AudioDevices.Acquire(new AudioOptions(), owner, () => { }));
            Assert.Equal(1, opens);

            Assert.Null(AudioDevices.Acquire(new AudioOptions { Backend = AudioOptions.SystemPlayer }, owner, () => { }));
            Assert.False(AudioDevices.IsSilent(new AudioOptions { Backend = AudioOptions.SystemPlayer }));
            Assert.Null(AudioDevices.Acquire(new AudioOptions { Backend = AudioOptions.Off }, owner, () => { }));
            Assert.True(AudioDevices.IsSilent(new AudioOptions { Backend = AudioOptions.Off }));
            Assert.Equal(1, opens); // neither of the two backends touched the OpenAL one

            Assert.NotNull(AudioDevices.Acquire(new AudioOptions { Backend = "from-a-newer-build" }, owner, () => { })); // an unknown id is OpenAL
            AudioDevices.Shutdown();
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AMissingOpenAlLibraryFallsBackWithOneMessageNamingTheFile()
    {
        await HeadlessApp.Run(() =>
        {
            AudioDevices.Shutdown();
            AudioDevices.Open = (_, _) => null;
            AudioDevices.LibraryPresent = () => false;
            var seen = new List<string>();
            AudioDevices.LibraryMissing += seen.Add;
            try
            {
                var options = new AudioOptions();
                Assert.Null(AudioDevices.Acquire(options, new object(), () => { }));
                Assert.Null(AudioDevices.Acquire(options with { BufferMs = 200 }, new object(), () => { }));
                Assert.Equal([AudioDevices.LibraryFile], seen); // told once, and the caller plays through the system player

                AudioDevices.Shutdown();
                Assert.Null(AudioDevices.Acquire(new AudioOptions { Backend = AudioOptions.SystemPlayer }, new object(), () => { }));
                Assert.Single(seen); // the system player needs no library
            }
            finally { AudioDevices.LibraryMissing -= seen.Add; }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task WithoutADeviceThePlayButtonFallsBackToTheSystemPlayer()
    {
        await HeadlessApp.Run(async () =>
        {
            AudioDevices.Shutdown();
            AudioDevices.Open = (_, _) => null; // a machine without sound hardware
            string? opened = null;
            var view = new SoundPlayerView(Sound(1), path => { opened = path; return true; });
            await view.Transport!.PlayPauseAsync();
            Assert.NotNull(opened);
            Assert.EndsWith(".wav", opened, StringComparison.Ordinal);
            Assert.False(view.Transport.IsPlaying);
        });
    }

    [Fact]
    public async Task PlayingAfterCloseDoesNotTakeTheDeviceAgain()
    {
        await HeadlessApp.Run(async () =>
        {
            var output = UseNullDevice();
            var window = new AudioEditorWindow("Edit audio", Wav(2), _ => { });
            window.Show();
            window.Close();
            await window.Transport.PlayRegionAsync();
            Assert.False(output.IsActive);
            AudioDevices.Shutdown();
        });
    }

    [Fact]
    public async Task TheAudioEditorPreviewsTheSelectedRangeAndStopsWhenItCloses()
    {
        await HeadlessApp.Run(async () =>
        {
            var output = UseNullDevice();
            var window = new AudioEditorWindow("Edit audio", Wav(2), _ => { });
            window.Show();
            try
            {
                Find<NumericUpDown>((Control)window.Content!, "AudioStart").Value = 0.5m;
                Find<NumericUpDown>((Control)window.Content!, "AudioEnd").Value = 1.0m;
                await window.Transport.PlayRegionAsync();
                Assert.Equal(0.5, output.Position, 3);
                Assert.True(output.IsPlaying);
            }
            finally { window.Close(); }

            Assert.False(output.IsActive); // closing the window stopped the sound
            AudioDevices.Shutdown();
        });
    }

    [Fact]
    public async Task TheSoundPageStoresDeviceVolumeAndBufferAndTheOptionsAreClamped()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            AudioDevices.List = () => ["Speakers", "Headset"];
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Registry.Execute("settings.open");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                T Named<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

                var device = Named<ComboBox>("AudioDevice");
                Assert.Equal(["Default", "Speakers", "Headset"], device.Items.Cast<string>());
                device.SelectedIndex = 2;
                Named<NumericUpDown>("AudioVolumeSetting").Value = 30;
                Named<NumericUpDown>("AudioBufferSetting").Value = 250;
                Assert.Equal(new AudioOptions { Device = "Headset", Volume = 30, BufferMs = 250 }, window.Workspaces.Settings.Current.Audio);
                Named<CheckBox>("ImageOptLayerAutosave").IsChecked = false;
                Assert.False(window.Workspaces.Settings.Current.Image.LayerAutosave);
                var backend = Named<ComboBox>("AudioBackend");
                Assert.Equal(3, backend.Items.Count);
                backend.SelectedIndex = 2;
                Assert.Equal(AudioOptions.Off, window.Workspaces.Settings.Current.Audio.Backend);
                Assert.Equal("Headset", window.Workspaces.Settings.Current.Audio.Device); // switching the backend keeps the device choice
            }
            finally { window.Close(); }
        });

        var clamped = new AppSettings { Audio = new AudioOptions { Volume = 900, BufferMs = 1 } };
        Assert.Equal((100, AudioOptions.MinBufferMs), (clamped.Clamped().Audio.Volume, clamped.Clamped().Audio.BufferMs));
    }

    [Fact]
    public async Task TheMissingLibraryNoteStaysHiddenByItsOwnStateWhenTheSearchFilterRuns()
    {
        using var dir = new TempDir();
        foreach (var present in new[] { true, false })
        {
            await HeadlessApp.Run(() =>
            {
                AudioDevices.LibraryPresent = () => present;
                var window = TabsWorkspaceTests.NewWindow(dir);
                try
                {
                    window.Registry.Execute("settings.open");
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    var note = window.GetVisualDescendants().OfType<TextBlock>().Single(c => c.Name == "AudioBackendNote");
                    Assert.Equal(!present, !note.Classes.Contains("selfHidden")); // the filter marks the section, not the note's own state
                }
                finally { window.Close(); }
            });
        }
    }

    [Fact]
    public async Task TheShortcutPageAddsAndRemovesAliases()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Registry.Execute("settings.open");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Settings.ShortcutsEditor Editor() { Dispatcher.UIThread.RunJobs(); return window.GetVisualDescendants().OfType<Settings.ShortcutsEditor>().Single(); } // the page is rebuilt when settings change
                Assert.Equal(["B", "OemPeriod"], window.Registry.Get("img.tool.pencil").Aliases);
                Editor().Set("img.tool.eraser", int.MaxValue, "K");
                Assert.Equal(["E", "K"], window.Registry.Get("img.tool.eraser").Gestures);
                Assert.Equal(["K"], window.Workspaces.Settings.Current.ShortcutAliases["img.tool.eraser"]);

                Editor().Set("img.tool.eraser", int.MaxValue, "B"); // the pencil's alias
                Assert.Contains("img.tool.pencil", window.Registry.UsersOf("B", "img.tool.eraser").Select(c => c.Id));
                Editor().Set("img.tool.eraser", 2, null); // remove the alias B
                Editor().Set("img.tool.eraser", 1, null); // and K
                Assert.DoesNotContain("img.tool.eraser", window.Workspaces.Settings.Current.ShortcutAliases.Keys);

                Editor().Set("img.tool.pencil", 1, null); // drop B from the pencil, keeping the period
                Assert.Equal(["OemPeriod"], window.Registry.Get("img.tool.pencil").Aliases);
                Editor().Set("img.tool.pencil", int.MaxValue, "B");
                Assert.Equal(["OemPeriod", "B"], window.Registry.Get("img.tool.pencil").Aliases);
                Assert.Equal(["OemPeriod", "B"], window.Workspaces.Settings.Current.ShortcutAliases["img.tool.pencil"]);
            }
            finally { window.Close(); }
        });
    }
}
