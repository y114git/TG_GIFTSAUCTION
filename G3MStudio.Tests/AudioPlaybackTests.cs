using Avalonia;
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

    internal static GameMakerSound Sound(double seconds) => new() { Name = new GameMakerString("snd"), AudioFile = new GameMakerEmbeddedAudio { Data = Wav(seconds) }, AudioLength = (float)seconds };

    internal static T Find<T>(Control root, string name) where T : Control =>
        Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(root).OfType<T>().Single(c => c.Name == name);

    internal static NullAudioOutput UseNullDevice()
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
    public void TheFeederTakesANewRangeWhileItReadsAndKeepsItsPlace()
    {
        var feeder = new PcmFeeder(Clip(1));
        var block = new short[400];
        feeder.Begin(0.25, 0.5); // frames 2000 to 4000
        Assert.Equal((300, 2000L), feeder.Read(block, 300));
        Assert.False(feeder.SetRange(0.25, 0.5)); // the same range is no change
        Assert.True(feeder.SetRange(0.25, 0.75)); // frames 2000 to 6000
        Assert.Equal((2000L, 6000L), feeder.Range);
        Assert.Equal(2300, feeder.Next); // the next block stays where it was
        Assert.Equal((300, 2300L), feeder.Read(block, 300));

        Assert.True(feeder.SetRange(0.5, 0.75)); // the start moved past the next block: reading goes on from the new start
        Assert.Equal((300, 4000L), feeder.Read(block, 300));

        feeder.Loop = true;
        Assert.True(feeder.SetRange(0.5, 0.55)); // frames 4000 to 4400, the next block is at 4300
        Assert.Equal(300, feeder.Read(new short[300], 300).Frames);
        Assert.Equal(4000 + 200, feeder.Next); // wrapped at the new end
        Assert.True(feeder.SetRange(0, 0)); // cleared: the whole clip
        Assert.Equal((0L, 8000L), feeder.Range);
    }

    [Fact]
    public async Task TheFeederCanBeReadAndChangedFromTwoThreads()
    {
        var feeder = new PcmFeeder(Clip(2)) { Loop = true };
        feeder.Begin(0, 0);
        var (running, reads) = (true, 0);
        var reader = Task.Run(() =>
        {
            var block = new short[256];
            long frames = 0;
            while (Volatile.Read(ref running))
            {
                var (count, from) = feeder.Read(block, 256);
                Assert.InRange(from, 0, 16000);
                frames += count;
                Interlocked.Increment(ref reads);
            }

            return frames;
        }, TestContext.Current.CancellationToken);
        for (var i = 0; i < 2_000_000 && Volatile.Read(ref reads) < 500; i++) feeder.SetRange(i % 7 * 0.1, i % 3 == 0 ? 0 : 0.9 + i % 5 * 0.2);
        Volatile.Write(ref running, false);
        Assert.True(await reader > 0);
    }

    [Fact]
    public void ARangeChangeKeepsAPositionInsideLoopsAtTheNewEndAndMovesOneOutsideToTheStart()
    {
        using var output = new NullAudioOutput();
        output.Load(Clip(4));
        output.SetRange(1, 2); // nothing plays: ignored
        Assert.False(output.IsActive);

        output.Play(1, 2);
        output.Advance(0.5); // at 1.5
        output.SetRange(1, 3); // longer: the sound goes on
        Assert.Equal(1.5, output.Position, 3);
        output.Advance(1.2); // 2.7, past the old end
        Assert.Equal(2.7, output.Position, 3);
        Assert.True(output.IsPlaying);

        output.Loop = true;
        output.SetRange(1, 2.5); // the playhead at 2.7 is outside: back to the start of the region
        Assert.Equal(1, output.Position, 3);
        output.Advance(1.7); // 2.7 wraps at the new end 2.5 to 1.2
        Assert.Equal(1.2, output.Position, 3);

        output.Pause();
        output.SetRange(3, 3.5); // paused and outside: it is at the new start when it goes on
        Assert.Equal(3, output.Position, 3);
        Assert.False(output.IsPlaying);
        output.Resume();
        output.SetRange(0, 0); // cleared: the whole clip, from where it is
        Assert.Equal(3, output.Position, 3);
        output.Loop = false;
        output.Advance(0.9);
        Assert.Equal(3.9, output.Position, 3);
        output.Advance(0.2);
        Assert.False(output.IsActive); // played on to the end of the clip
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
            // the fallback extracts the file off the UI thread, so the system player is asked a moment later
            for (var i = 0; i < 200 && opened is null; i++)
            {
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                await Task.Delay(10);
            }

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

    [Fact]
    public void ThePeaksGiveTheLowestAndHighestSamplePerColumnAndStayInRange()
    {
        var mono = new WavePeaks(new PcmAudio([0.5f, -1f, 0.25f, 0f, 0.75f], 1, Rate)).Columns(0, 1, 5);
        Assert.Equal([0.5f, -1f, 0.25f, 0f, 0.75f], mono[0].Max);
        Assert.Equal(mono[0].Max, mono[0].Min); // one sample per column

        var stereo = new WavePeaks(new PcmAudio([0.1f, -0.9f, 0.2f, 0.3f], 2, Rate)).Columns(0, 1, 1); // both frames in one column, a lane per channel
        Assert.Equal([0.1f, 0.2f], [stereo[0].Min[0], stereo[0].Max[0]]);
        Assert.Equal([-0.9f, 0.3f], [stereo[1].Min[0], stereo[1].Max[0]]);

        var bad = new WavePeaks(new PcmAudio([float.NaN, 3f, -7f, 0.2f], 1, Rate)).Columns(0, 1, 2);
        Assert.Equal([1f, -1f, 1f, 0.2f], [.. bad[0].Min, .. bad[0].Max]); // out of range samples are clipped, NaN is ignored
        var none = new WavePeaks(new PcmAudio([], 1, Rate)).Columns(0, 1, 3);
        Assert.Equal(new float[3], none[0].Max); // no audio draws a flat line
    }

    internal static async Task Until(Func<bool> done)
    {
        for (var i = 0; i < 200 && !done(); i++)
        {
            await Task.Delay(25);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(done());
    }

    [Fact]
    public async Task TheWaveformOfAnOggAndTheDecodedLengthAppearWithoutPressingPlay()
    {
        await HeadlessApp.Run(async () =>
        {
            var ogg = AudioCodec.Encode(Clip(2), "ogg");
            var view = new SoundPlayerView("snd", ogg, _ => true);
            var window = new Window { Width = 600, Height = 500, Content = view };
            window.Show();
            try
            {
                var wave = Find<WaveformView>(view, "Waveform");
                Assert.Null(wave.Source); // nothing is known before the decode ends
                await Until(() => wave.Source is not null);
                var columns = wave.Source!.Columns(0, 1, 100)[0].Max;
                Assert.True(columns[^5] > columns[5]); // the clip is a ramp: the right end is louder
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task OneDecodeServesTwoPlayPressesAndItsLengthReplacesTheHeaderLength()
    {
        await HeadlessApp.Run(async () =>
        {
            UseNullDevice();
            var calls = 0;
            var transport = new AudioTransport(1, () => { Interlocked.Increment(ref calls); Thread.Sleep(50); return new PcmAudio(new float[3 * Rate], 1, Rate); }, () => new AudioOptions());
            try
            {
                await Task.WhenAll(transport.PlayPauseAsync(), transport.PlayPauseAsync()); // two presses while the decode runs
                Assert.Equal(1, calls);
                Assert.Contains("/ 0:03.0", transport.TimeText, StringComparison.Ordinal); // the header said 1 s, the audio is 3 s
            }
            finally
            {
                transport.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task DraggingTheSeekThumbMovesOnlyTheCursorAndPlayingContinuesFromWhereItIsLetGo()
    {
        await HeadlessApp.Run(async () =>
        {
            var output = UseNullDevice();
            var view = new SoundPlayerView(Sound(2), _ => true);
            var window = new Window { Width = 600, Height = 500, Content = view };
            window.Show();
            try
            {
                var transport = view.Transport!;
                var seek = Find<Slider>(view, "AudioSeek");
                await transport.PlayPauseAsync();
                output.Advance(0.5);
                transport.Tick();
                Dispatcher.UIThread.RunJobs();
                var bounds = seek.TranslatePoint(new Point(0, seek.Bounds.Height / 2), window)!.Value;
                Point At(double fraction) => new(bounds.X + 12 + (seek.Bounds.Width - 24) * fraction, bounds.Y);

                Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, At(0.25), Avalonia.Input.MouseButton.Left);
                Avalonia.Headless.HeadlessWindowExtensions.MouseMove(window, At(0.75));
                Assert.InRange(seek.Value, 1.2, 1.8);
                Assert.Equal(0.5, output.Position, 3); // the sound has not jumped yet
                output.Advance(0.1);
                transport.Tick(); // the timer runs during the drag and must not pull the thumb back
                Assert.InRange(seek.Value, 1.2, 1.8);
                Assert.Contains("0:01.", transport.TimeText, StringComparison.Ordinal);

                Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, At(0.75), Avalonia.Input.MouseButton.Left);
                Assert.Equal(seek.Value, output.Position, 2);
                Assert.True(output.IsPlaying);
                output.Advance(0.2);
                transport.Tick();
                Assert.Equal(output.Position, seek.Value, 2); // the timer drives the thumb again
            }
            finally
            {
                window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task ALostPointerCaptureEndsTheSeekDragSoTheBarFollowsThePlayingSoundAgain()
    {
        await HeadlessApp.Run(async () =>
        {
            var output = UseNullDevice();
            var view = new SoundPlayerView(Sound(2), _ => true);
            var window = new Window { Width = 600, Height = 500, Content = view };
            window.Show();
            try
            {
                var transport = view.Transport!;
                var seek = Find<Slider>(view, "AudioSeek");
                await transport.PlayPauseAsync();
                Dispatcher.UIThread.RunJobs();
                var bounds = seek.TranslatePoint(new Point(0, seek.Bounds.Height / 2), window)!.Value;
                Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, new Point(bounds.X + seek.Bounds.Width / 2, bounds.Y), Avalonia.Input.MouseButton.Left);
                var thumb = seek.GetVisualDescendants().OfType<Avalonia.Controls.Primitives.Thumb>().Single();
                // Alt+Tab mid drag: the capture goes, the release never comes.
                thumb.RaiseEvent(new Avalonia.Input.PointerCaptureLostEventArgs(thumb, new Avalonia.Input.Pointer(Avalonia.Input.Pointer.GetNextFreeId(), Avalonia.Input.PointerType.Mouse, true)));
                output.Advance(0.3);
                transport.Tick();
                Assert.Equal(output.Position, seek.Value, 2);
            }
            finally
            {
                window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task ADecodeThatFailsAnyWayFallsBackToTheSystemPlayerEveryTime()
    {
        await HeadlessApp.Run(async () =>
        {
            UseNullDevice();
            var told = 0;
            var transport = new AudioTransport(1, () => throw new IOException("unreadable"), () => new AudioOptions()) { NoDevice = () => told++ };
            try
            {
                await transport.PlayPauseAsync();
                await transport.PlayPauseAsync();
                Assert.Equal(2, told);
                Assert.False(transport.IsPlaying);
            }
            finally { AudioDevices.Shutdown(); }
        });
    }

    [Fact]
    public async Task ThePlayRegionIsClampedToTheDecodedLengthNotTheHeaderLength()
    {
        await HeadlessApp.Run(async () =>
        {
            var output = UseNullDevice();
            // The header says 1 s, the audio is 3 s long: a region at 1.5 to 2.5 s is valid once the audio is known.
            var transport = new AudioTransport(1, () => Clip(3), () => new AudioOptions(), () => (1.5, 2.5));
            try
            {
                await transport.PlayRegionAsync();
                Assert.Equal(1.5, output.Position, 3);
                transport.Stop();
                await transport.PlayPauseAsync();
                Assert.Equal(1.5, output.Position, 3);
            }
            finally { AudioDevices.Shutdown(); }
        });
    }
}
