using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using G3MStudio.Audio;
using G3MStudio.Core.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>Zoom, scroll and resize of the waveform: the pointer maps to the same time at every zoom and scroll, the peaks come from the samples of the visible range, and a long sound does not stall.</summary>
public sealed class WaveformZoomTests
{
    private const int Rate = 8000;

    private sealed record Rig(Window Window, SoundPlayerView View, AudioTransport Transport, WaveformView Wave, NullAudioOutput Output)
    {
        public Point At(double fraction) => new(Wave.TranslatePoint(default, Window)!.Value.X + Wave.XOf(fraction), Wave.TranslatePoint(default, Window)!.Value.Y + 32);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "The sound lives as long as the test window.")]
    private static Rig Open(double seconds = 2)
    {
        var output = AudioPlaybackTests.UseNullDevice();
        var view = new SoundPlayerView(AudioPlaybackTests.Sound(seconds), _ => true);
        var window = new Window { Width = 700, Height = 600, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return new Rig(window, view, view.Transport!, AudioPlaybackTests.Find<WaveformView>(view, "Waveform"), output);
    }

    private static async Task Decoded(Rig rig)
    {
        await AudioPlaybackTests.Until(() => rig.Wave.Source is not null);
        rig.Window.UpdateLayout();
    }

    [Fact]
    public void AColumnOfAZoomedOutRangeCoversItsSamplesAndAZoomedInOneIsExact()
    {
        var clip = new PcmAudio([.. Enumerable.Range(0, 200_000).Select(i => (float)Math.Sin(i * i * 0.0007))], 1, Rate); // uneven, noise like samples
        var peaks = new WavePeaks(clip);
        foreach (var (start, span, columns) in new[] { (0.0, 1.0, 300), (0.3, 0.4, 250), (0.5, 0.01, 200), (0.9, 0.0005, 100) })
        {
            var (min, max) = peaks.Columns(start, span, columns)[0];
            for (var i = 0; i < columns; i++)
            {
                var from = (int)Math.Floor(start * clip.Frames + span * clip.Frames * i / columns);
                var to = Math.Max(from + 1, (int)Math.Ceiling(start * clip.Frames + span * clip.Frames * (i + 1) / columns));
                var slice = clip.Samples.AsSpan(from, Math.Min(to, clip.Frames) - from);
                Assert.True(min[i] <= slice.ToArray().Min() + 1e-6f && max[i] >= slice.ToArray().Max() - 1e-6f, $"column {i} of {start}/{span}");
                if (to - from < 2 * WavePeaks.Block) Assert.Equal((slice.ToArray().Min(), slice.ToArray().Max()), (min[i], max[i])); // read from the samples themselves
            }
        }
    }

    [Fact]
    public void AColumnOfSeveralBlocksIgnoresTheSamplesOutsideItsSlice()
    {
        var clip = new PcmAudio(new float[4 * WavePeaks.Block], 1, Rate);
        clip.Samples[0] = 0.9f; // in the first block, before the slice
        clip.Samples[700] = 0.8f; // in the third block, after the slice
        var peaks = new WavePeaks(clip);
        var (min, max) = peaks.Columns(1.0 / clip.Frames, 512.0 / clip.Frames, 1)[0]; // the slice [1, 513) reaches into the partial blocks 0 and 2
        Assert.Equal((0f, 0f), (min[0], max[0]));
        clip.Samples[300] = -0.5f; // a whole block inside the slice still counts
        clip.Samples[1] = 0.7f;
        (min, max) = new WavePeaks(clip).Columns(1.0 / clip.Frames, 512.0 / clip.Frames, 1)[0];
        Assert.Equal((-0.5f, 0.7f), (min[0], max[0]));
    }

    [Fact]
    public void ATenMinuteSoundBuildsAndDrawsAnyRangeWithoutStalling()
    {
        var clip = new PcmAudio(new float[10 * 60 * 44100], 1, 44100);
        clip.Samples[300_000_000 / 20] = 0.9f;
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var peaks = new WavePeaks(clip);
        var built = watch.ElapsedMilliseconds;
        watch.Restart();
        for (var i = 0; i < 20; i++) peaks.Columns(i / 40.0, 1 - i / 40.0, 1600); // 20 redraws at different zooms
        Assert.True(watch.ElapsedMilliseconds < 1500, $"20 redraws took {watch.ElapsedMilliseconds} ms (the build took {built} ms)");
        Assert.Contains(peaks.Columns(0, 1, 1600)[0].Max, v => v > 0.89f);
    }

    [Theory]
    [InlineData(0.0, 1.0)]
    [InlineData(0.0, 0.25)]
    [InlineData(0.4, 0.25)]
    [InlineData(0.9, 0.1)]
    [InlineData(0.3, 0.02)]
    public async Task AClickAndADragMapToTheSameTimeAtEveryZoomAndScroll(double start, double span)
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = Open(2);
            try
            {
                await Decoded(rig);
                await rig.Transport.PlayPauseAsync();
                rig.Wave.SetView(start, span);
                rig.Window.UpdateLayout();
                Assert.Equal(start, rig.Wave.ViewStart, 9);
                Assert.Equal(span, rig.Wave.ViewSpan, 9);

                var click = start + span * 0.37; // a place inside the view
                rig.Window.MouseDown(rig.At(click), MouseButton.Left);
                rig.Window.MouseUp(rig.At(click), MouseButton.Left);
                Assert.Equal(click * 2, rig.Output.Position, 4);
                Assert.Equal(click, rig.Wave.FractionAt(rig.Wave.XOf(click)), 9);

                var (from, to) = (start + span * 0.2, start + span * 0.7);
                rig.Window.MouseDown(rig.At(from), MouseButton.Left);
                rig.Window.MouseMove(rig.At((from + to) / 2));
                rig.Window.MouseMove(rig.At(to));
                rig.Window.MouseUp(rig.At(to), MouseButton.Left);
                var region = rig.Wave.Region!.Value; // the owner has not answered: the control shows the drag as it was made
                Assert.InRange(region.Start * 2, from * 2 - 0.006, from * 2 + 0.006); // the ends of a region are rounded to hundredths of a second
                Assert.InRange(region.End * 2, to * 2 - 0.006, to * 2 + 0.006);
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task TheEdgesOfTheRegionAreGrabbedWhereTheyAreDrawnWhenZoomedAndScrolled()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = Open(2);
            try
            {
                await Decoded(rig);
                Find<NumericUpDown>(rig.View, "SoundRegionStart").Value = 0.6m; // 0.3 of the sound
                Find<NumericUpDown>(rig.View, "SoundRegionEnd").Value = 1.2m; // 0.6 of the sound
                rig.Wave.SetView(0.25, 0.25);
                rig.Window.UpdateLayout();
                rig.Window.MouseMove(rig.At(0.3));
                Assert.Same(WaveformView.Edge, rig.Wave.Cursor);
                rig.Window.MouseMove(rig.At(0.4));
                Assert.Same(WaveformView.Band, rig.Wave.Cursor); // inside the region, away from its edges
                rig.Window.MouseMove(rig.At(0.26));
                Assert.Same(WaveformView.Pick, rig.Wave.Cursor);

                rig.Window.MouseDown(rig.At(0.3), MouseButton.Left);
                rig.Window.MouseMove(rig.At(0.35));
                rig.Window.MouseUp(rig.At(0.35), MouseButton.Left);
                Assert.Equal(0.35 * 2, (double)Find<NumericUpDown>(rig.View, "SoundRegionStart").Value!, 2);
                Assert.Equal(1.2, (double)Find<NumericUpDown>(rig.View, "SoundRegionEnd").Value!, 2); // the other edge stays, though it is off screen
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    private static T Find<T>(Control root, string name) where T : Control => AudioPlaybackTests.Find<T>(root, name);

    [Fact]
    public async Task CtrlWheelZoomsAtThePointerAndTheWheelScrollsOnlyWhileZoomedIn()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = Open(2);
            try
            {
                await Decoded(rig);
                rig.Window.MouseWheel(rig.At(0.5), new Vector(0, -1)); // not zoomed: the wheel is not taken
                Assert.Equal((0.0, 1.0), (rig.Wave.ViewStart, rig.Wave.ViewSpan));

                var pointer = rig.At(0.75);
                rig.Window.MouseWheel(pointer, new Vector(0, 2), RawInputModifiers.Control);
                Assert.True(rig.Wave.ViewSpan < 0.7);
                Assert.Equal(0.75, rig.Wave.FractionAt(rig.Wave.XOf(0.75)), 9);
                Assert.Equal(0.75, rig.Wave.FractionAt(pointer.X - rig.Wave.TranslatePoint(default, rig.Window)!.Value.X), 2); // the time under the pointer did not move

                var before = rig.Wave.ViewStart;
                rig.Window.MouseWheel(pointer, new Vector(0, -1)); // down scrolls forward
                Assert.True(rig.Wave.ViewStart >= before);
                rig.Window.MouseWheel(pointer, new Vector(0, 1));
                rig.Window.MouseWheel(pointer, new Vector(0, 1));
                Assert.True(rig.Wave.ViewStart < before);

                rig.Window.MouseWheel(pointer, new Vector(0, -40), RawInputModifiers.Control); // far out: back to the whole sound
                Assert.Equal((0.0, 1.0), (rig.Wave.ViewStart, rig.Wave.ViewSpan));
                rig.Window.MouseWheel(pointer, new Vector(0, 200), RawInputModifiers.Control);
                Assert.Equal(rig.Wave.MinSpan, rig.Wave.ViewSpan, 9); // and no further in than about 16 frames
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task ThePlusKeyZoomsWithOrWithoutShiftAndShiftDoesNotTriggerTheOtherKeys()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = Open(2);
            try
            {
                await Decoded(rig);
                KeyEventArgs Press(Key key, KeyModifiers modifiers)
                {
                    var e = new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers };
                    rig.Transport.RaiseEvent(e);
                    return e;
                }

                Assert.True(Press(Key.OemPlus, KeyModifiers.Shift).Handled); // "+" is Shift+= on a US layout
                Assert.Equal(0.5, rig.Wave.ViewSpan, 9);
                Assert.True(Press(Key.OemPlus, KeyModifiers.None).Handled);
                Assert.Equal(0.25, rig.Wave.ViewSpan, 9);
                Assert.True(Press(Key.OemMinus, KeyModifiers.None).Handled);
                Assert.Equal(0.5, rig.Wave.ViewSpan, 9);
                Assert.False(Press(Key.OemPlus, KeyModifiers.Control).Handled); // other combinations are not ours
                Assert.Equal(0.5, rig.Wave.ViewSpan, 9);
                Assert.False(Press(Key.Space, KeyModifiers.Shift).Handled);
                Assert.False(Press(Key.R, KeyModifiers.Shift).Handled);
                Assert.False(rig.Transport.IsPlaying);
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task TheButtonsAndTheScrollBarZoomFitAndScrollAndSayWhyTheyAreOff()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = Open(2);
            try
            {
                var (zoomIn, zoomOut, fit, scroll) = (Find<Button>(rig.View, "ZoomIn"), Find<Button>(rig.View, "ZoomOut"), Find<Button>(rig.View, "ZoomFit"), Find<ScrollBar>(rig.View, "WaveScroll"));
                await Decoded(rig);
                Assert.True(zoomIn.IsEnabled);
                Assert.False(zoomOut.IsEnabled);
                Assert.False(fit.IsEnabled);
                Assert.NotEqual(ToolTip.GetTip(zoomIn), ToolTip.GetTip(zoomOut)); // an off button tells the owner why
                Assert.All([zoomIn, zoomOut, fit], button => Assert.True(ToolTip.GetShowOnDisabled(button))); // so the tip shows on the disabled ones
                Assert.False(scroll.IsVisible);

                zoomIn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                zoomIn.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(0.25, rig.Wave.ViewSpan, 9);
                Assert.True(scroll.IsVisible);
                Assert.True(zoomOut.IsEnabled && fit.IsEnabled);
                Assert.Equal(0.75, scroll.Maximum, 9);
                scroll.Value = 0.5;
                Assert.Equal(0.5, rig.Wave.ViewStart, 9);
                rig.Wave.Scroll(0.5);
                Assert.Equal(0.5 + 0.125, scroll.Value, 9); // the bar follows the view

                zoomOut.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(0.5, rig.Wave.ViewSpan, 9);
                fit.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.Equal((0.0, 1.0), (rig.Wave.ViewStart, rig.Wave.ViewSpan));
                Assert.False(scroll.IsVisible);
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task ZoomInIsOffWithATipWhileTheAudioIsStillDecoding()
    {
        await HeadlessApp.Run(async () =>
        {
            AudioPlaybackTests.UseNullDevice();
            using var gate = new ManualResetEventSlim();
            var transport = new AudioTransport(1, () => { gate.Wait(); return new PcmAudio(new float[Rate], 1, Rate); }, () => new G3MStudio.Core.Settings.AudioOptions());
            var window = new Window { Width = 700, Height = 600, Content = transport };
            window.Show();
            try
            {
                var zoomIn = Find<Button>(transport, "ZoomIn");
                Assert.False(zoomIn.IsEnabled);
                Assert.Equal(Localization.Loc.Current["audio.zoomWait"], ToolTip.GetTip(zoomIn));
                gate.Set();
                await AudioPlaybackTests.Until(() => zoomIn.IsEnabled);
            }
            finally
            {
                gate.Set();
                window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task ThePlayingCursorIsFollowedUntilTheUserScrolls()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = Open(4);
            try
            {
                await Decoded(rig);
                rig.Wave.SetView(0, 0.25); // 0 to 1 s
                await rig.Transport.PlayPauseAsync();
                rig.Output.Advance(1.5);
                rig.Transport.Tick();
                Assert.InRange(1.5 / 4, rig.Wave.ViewStart, rig.Wave.ViewStart + rig.Wave.ViewSpan); // the view moved on to the cursor
                Assert.True(rig.Wave.ViewStart > 0);

                rig.Wave.SetView(0, 0.25); // the user scrolls away: the cursor is let go
                rig.Output.Advance(1);
                rig.Transport.Tick();
                Assert.Equal(0, rig.Wave.ViewStart);

                rig.Transport.Stop();
                await rig.Transport.PlayPauseAsync(); // a new play follows again
                rig.Output.Advance(2.5);
                rig.Transport.Tick();
                Assert.InRange(2.5 / 4, rig.Wave.ViewStart, rig.Wave.ViewStart + rig.Wave.ViewSpan);
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task TheHandleUnderTheWaveformResizesItAndTheSizeIsKeptForTheNextOne()
    {
        await HeadlessApp.Run(() =>
        {
            var rig = Open(2);
            try
            {
                var handle = Find<Border>(rig.View, "ResizeHandle_preview.audio");
                var height = rig.Wave.Bounds.Height;
                var grab = handle.TranslatePoint(new Point(20, 4), rig.Window)!.Value;
                rig.Window.MouseDown(grab, MouseButton.Left);
                rig.Window.MouseMove(grab + new Vector(0, 60), RawInputModifiers.LeftMouseButton);
                rig.Window.MouseUp(grab + new Vector(0, 60), MouseButton.Left);
                rig.Window.UpdateLayout();
                Assert.Equal(height + 60, rig.Wave.Bounds.Height, 1);
                Assert.Equal(height + 60, DragHandle.Remembered("preview.audio")!.Value, 1);

                var second = Open(1);
                try
                {
                    Assert.Equal(height + 60, second.Wave.Height, 1); // the next sound opens as it was left
                }
                finally { second.Window.Close(); }
            }
            finally
            {
                DragHandle.Resize(rig.Wave, "preview.audio", 64);
                rig.Window.Close();
                AudioDevices.Shutdown();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheWaveformHeightDraggedLastIsHandedToTheSettingsAndEveryPlayerOpensAtTheSavedOne()
    {
        await HeadlessApp.Run(() =>
        {
            double? kept = null;
            AudioTransport.RememberWaveformHeight = height => kept = height;
            var view = new SoundPlayerView(AudioPlaybackTests.Sound(1), _ => true, () => new AudioOptions { WaveformHeight = 120 });
            var window = new Window { Width = 600, Height = 500, Content = view };
            window.Show();
            var wave = Find<WaveformView>(view, "Waveform");
            try
            {
                window.UpdateLayout();
                Assert.Equal(120, wave.Height); // from the settings, over the height of this session
                var grab = Find<Border>(view, "ResizeHandle_preview.audio").TranslatePoint(new Point(20, 4), window)!.Value;
                window.MouseDown(grab, MouseButton.Left);
                window.MouseMove(grab + new Vector(0, 30), RawInputModifiers.LeftMouseButton);
                Assert.Null(kept); // the settings are written once, when the drag ends
                window.MouseUp(grab + new Vector(0, 30), MouseButton.Left);
                Assert.Equal(150, kept!.Value, 1);
            }
            finally
            {
                AudioTransport.RememberWaveformHeight = null;
                DragHandle.Resize(wave, "preview.audio", 64);
                window.Close();
            }

            Assert.Equal(0, new AudioOptions().Clamped().WaveformHeight); // none saved: the built in height
            Assert.Equal(AudioOptions.MinWaveformHeight, new AudioOptions { WaveformHeight = 5 }.Clamped().WaveformHeight);
            Assert.Equal(AudioOptions.MaxWaveformHeight, new AudioOptions { WaveformHeight = 99999 }.Clamped().WaveformHeight);
            Assert.Equal(0, new AudioOptions { WaveformHeight = -3 }.Clamped().WaveformHeight);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task AStereoSoundHasTwoLanesOnlyWhenTheWaveformIsTallEnough()
    {
        await HeadlessApp.Run(() =>
        {
            var wave = new WaveformView { Width = 300, Height = 60, Source = new WavePeaks(new PcmAudio([.. Enumerable.Range(0, 2000).Select(i => i % 2 == 0 ? 0.5f : -0.5f)], 2, Rate)) };
            var window = new Window { Width = 400, Height = 300, Content = wave };
            window.Show();
            try
            {
                window.UpdateLayout();
                Assert.Equal(1, wave.LaneCount);
                wave.Height = 140;
                window.UpdateLayout();
                Assert.Equal(2, wave.LaneCount);
                wave.Source = new WavePeaks(new PcmAudio([0.5f, -0.5f], 1, Rate));
                Assert.Equal(1, wave.LaneCount); // a mono sound never splits
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheRegionAndThePlayheadAreNotDrawnOutsideTheControl()
    {
        await HeadlessApp.Run(() =>
        {
            var wave = new WaveformView { Width = 50, Height = 64, Region = (-0.5, 0.2), Playhead = -0.01 }; // the shade ends at x = 10; the cursor is 1.5 to 0.5 px left of the control
            Assert.True(wave.ClipToBounds);
            var canvas = new Canvas { Width = 150, Height = 64, Children = { wave } };
            Canvas.SetLeft(wave, 50);
            canvas.Measure(new Size(150, 64));
            canvas.Arrange(new Rect(0, 0, 150, 64));
            using var target = new RenderTargetBitmap(new PixelSize(150, 64));
            target.Render(canvas);
            using var stream = new MemoryStream();
            target.Save(stream, new PngBitmapEncoderOptions());
            using var image = new ImageMagick.MagickImage(stream.ToArray());
            using var pixels = image.GetPixels();
            double Alpha(int x, int y) => pixels.GetPixel(x, y).ToArray()[3];
            Assert.Equal(0, Alpha(49, 10)); // left of the control: nothing, not even the edge of the cursor
            Assert.Equal(0, Alpha(30, 10)); // the shade would reach here without the clip
            Assert.True(Alpha(55, 10) > 0); // the visible part of the region is drawn
            Assert.Equal(0, Alpha(65, 10));
            return Task.CompletedTask;
        });
    }
}
