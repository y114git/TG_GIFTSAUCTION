using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using G3MStudio.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The play region of the sound player: a click seeks, a drag selects, the edges drag, Clear and Escape, play and loop stay inside it, and the toolbar fits narrow windows.</summary>
public sealed class SoundRegionTests
{
    private sealed record Rig(Window Window, SoundPlayerView View, AudioTransport Transport, WaveformView Wave, NumericUpDown Start, NumericUpDown End)
    {
        public double Origin => Wave.TranslatePoint(default, Window)!.Value.X;

        public Point At(double fraction, double dx = 0) => new(Origin + Wave.Bounds.Width * fraction + dx, Wave.TranslatePoint(default, Window)!.Value.Y + 32);

        public void Down(double fraction, double dx = 0) => Window.MouseDown(At(fraction, dx), MouseButton.Left);

        public void Move(double fraction, double dx = 0) => Window.MouseMove(At(fraction, dx));

        public void Up(double fraction, double dx = 0) => Window.MouseUp(At(fraction, dx), MouseButton.Left);

        public (double Start, double End) Region => ((double)Start.Value!, (double)End.Value!);
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "The sound lives as long as the test window.")]
    private static Rig Open(double seconds = 2)
    {
        var view = new SoundPlayerView(AudioPlaybackTests.Sound(seconds), _ => true);
        var window = new Window { Width = 700, Height = 500, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return new Rig(window, view, view.Transport!, AudioPlaybackTests.Find<WaveformView>(view, "Waveform"), AudioPlaybackTests.Find<NumericUpDown>(view, "SoundRegionStart"), AudioPlaybackTests.Find<NumericUpDown>(view, "SoundRegionEnd"));
    }

    [Fact]
    public async Task AClickSeeksAndAShortDragIsAClickWhileALongDragSelectsTheRegionLive()
    {
        await HeadlessApp.Run(async () =>
        {
            var output = AudioPlaybackTests.UseNullDevice();
            var rig = Open();
            try
            {
                await rig.Transport.PlayPauseAsync();
                rig.Down(0.5);
                rig.Up(0.5);
                Assert.Equal(1.0, output.Position, 1);
                Assert.Equal((0, 0), rig.Region); // a click selects nothing

                rig.Down(0.3);
                rig.Move(0.3, 3); // under the slop
                rig.Up(0.3, 3);
                Assert.Equal(0.6, output.Position, 1);
                Assert.Equal((0, 0), rig.Region);

                rig.Down(0.1);
                rig.Move(0.5);
                Assert.Equal(0.2, rig.Region.Start, 1); // live: the fields follow the pointer before it is released
                Assert.Equal(1.0, rig.Region.End, 1);
                rig.Move(0.75);
                var seeked = output.Position;
                rig.Up(0.75);
                Assert.Equal(0.2, rig.Region.Start, 1);
                Assert.Equal(1.5, rig.Region.End, 1);
                Assert.Equal(seeked, output.Position, 3); // a drag does not seek
                Assert.NotNull(rig.Wave.Region);
                Assert.Contains("[0:00.2 - 0:01.5]", rig.Transport.TimeText, StringComparison.Ordinal);

                rig.Down(0.9); // dragging right to left selects as well
                rig.Move(0.8);
                rig.Up(0.8);
                Assert.Equal(1.6, rig.Region.Start, 1);
                Assert.Equal(1.8, rig.Region.End, 1);
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task TheEdgesOfTheRegionDragAndCrossingTheOtherEdgeSwapsThem()
    {
        await HeadlessApp.Run(() =>
        {
            AudioPlaybackTests.UseNullDevice();
            var rig = Open();
            try
            {
                rig.Start.Value = 0.5m;
                rig.End.Value = 1.5m;
                rig.Window.UpdateLayout();
                Assert.NotNull(rig.Wave.Region);

                rig.Move(0.25, 2); // near the start edge: the cursor turns into the resize cursor
                Assert.Same(WaveformView.Edge, rig.Wave.Cursor);
                rig.Move(0.5);
                Assert.Same(WaveformView.Band, rig.Wave.Cursor); // inside the band: the whole region can be moved
                rig.Move(0.9);
                Assert.Same(WaveformView.Pick, rig.Wave.Cursor);

                rig.Move(0.25, 2);
                rig.Down(0.25, 2);
                rig.Move(0.1);
                rig.Up(0.1);
                Assert.Equal(0.2, rig.Region.Start, 1);
                Assert.Equal(1.5, rig.Region.End, 3); // the other edge stayed

                rig.Down(0.75, 3); // the end edge, dragged left past the start edge
                rig.Move(0.0);
                rig.Up(0.0);
                Assert.Equal(0.0, rig.Region.Start, 3);
                Assert.Equal(0.2, rig.Region.End, 1);
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ClearRegionAndADoubleClickRemoveTheRegionAndEscapeCancelsASelection()
    {
        await HeadlessApp.Run(() =>
        {
            AudioPlaybackTests.UseNullDevice();
            var rig = Open();
            try
            {
                rig.Start.Value = 0.5m;
                rig.End.Value = 1.5m;
                AudioPlaybackTests.Find<Button>(rig.View, "AudioClearRegion").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal((0, 0), rig.Region);
                Assert.Null(rig.Wave.Region);

                rig.Start.Value = 0.5m;
                rig.End.Value = 1.5m;
                rig.Down(0.9);
                rig.Up(0.9);
                var clicked = rig.Transport.Position;
                rig.Down(0.9);
                rig.Up(0.9);
                Assert.Equal((0, 0), rig.Region); // the second press of a double click clears
                Assert.Equal(clicked, rig.Transport.Position, 3);

                rig.Start.Value = 0.5m;
                rig.End.Value = 1.5m;
                rig.Down(0.8);
                rig.Move(0.95);
                Assert.Equal(1.6, rig.Region.Start, 1);
                rig.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                Assert.Equal((0.5, 1.5), rig.Region); // the old region is back
                rig.Move(0.3); // the rest of the gesture is ignored
                rig.Up(0.3);
                Assert.Equal((0.5, 1.5), rig.Region);
                Assert.Equal(clicked, rig.Transport.Position, 3); // the cancelled gesture did not seek either
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EscapeWithoutADragAndADragTooShortToRoundLeaveTheRegionAlone()
    {
        await HeadlessApp.Run(() =>
        {
            AudioPlaybackTests.UseNullDevice();
            var rig = Open(0.05); // a bar pixel is a thousandth of a second here
            try
            {
                rig.Start.Value = 0.7m; // typed, but no end yet: not a region
                rig.Down(0.5);
                rig.Window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, null);
                rig.Up(0.5);
                Assert.Equal(0.7m, rig.Start.Value);

                rig.Start.Value = 0.01m;
                rig.End.Value = 0.04m;
                rig.Down(0.41);
                rig.Move(0.43);
                rig.Up(0.43); // 0.0205 and 0.0215 seconds both round to 0.02
                Assert.Equal((0.01, 0.04), rig.Region);
                Assert.NotNull(rig.Wave.Region);
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task PlayAndLoopStayInsideTheRegion()
    {
        await HeadlessApp.Run(async () =>
        {
            var output = AudioPlaybackTests.UseNullDevice();
            var rig = Open();
            try
            {
                rig.Start.Value = 1.0m;
                rig.End.Value = 1.5m;
                await rig.Transport.PlayPauseAsync(); // plain Play, not Play region
                Assert.Equal(1.0, output.Position, 3);
                output.Advance(0.6); // past the end
                rig.Transport.Tick();
                Assert.False(output.IsActive);

                AudioPlaybackTests.Find<CheckBox>(rig.View, "AudioLoop").IsChecked = true;
                await rig.Transport.PlayPauseAsync();
                output.Advance(0.6); // 1.6 wraps to 1.1 inside the region
                Assert.Equal(1.1, output.Position, 3);
                rig.Transport.Stop();

                rig.Transport.SeekTo(1.2); // inside: play continues from the bar
                await rig.Transport.PlayPauseAsync();
                Assert.Equal(1.2, output.Position, 3);
                rig.Transport.Stop();
                rig.Transport.SeekTo(0.2); // outside: play starts at the region start
                await rig.Transport.PlayPauseAsync();
                Assert.Equal(1.0, output.Position, 3);
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task ARegionTypedOrClearedWhileTheSoundPlaysChangesTheRangeWithoutRestartingIt()
    {
        await HeadlessApp.Run(async () =>
        {
            var output = AudioPlaybackTests.UseNullDevice();
            var rig = Open(4);
            try
            {
                await rig.Transport.PlayPauseAsync();
                output.Advance(0.5);
                rig.Start.Value = 1.0m;
                rig.End.Value = 2.0m; // the playhead at 0.5 is outside the new region: it jumps to the start of the region
                Assert.True(output.IsPlaying);
                Assert.Equal(1.0, output.Position, 3);

                output.Advance(0.5); // 1.5, inside
                rig.End.Value = 3.0m; // a longer region: no jump, and the sound goes on past the old end
                Assert.Equal(1.5, output.Position, 3);
                output.Advance(1.0);
                Assert.Equal(2.5, output.Position, 3);
                Assert.True(output.IsPlaying);

                AudioPlaybackTests.Find<CheckBox>(rig.View, "AudioLoop").IsChecked = true;
                rig.End.Value = 2.25m; // loop on, the playhead is past the new end: back to the region start, without a stop
                Assert.Equal(1.0, output.Position, 3);
                output.Advance(1.5); // 2.5 wraps at the new end 2.25 to 1.25
                Assert.Equal(1.25, output.Position, 3);

                AudioPlaybackTests.Find<Button>(rig.View, "AudioClearRegion").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(1.25, output.Position, 3); // cleared: it keeps playing from where it is
                AudioPlaybackTests.Find<CheckBox>(rig.View, "AudioLoop").IsChecked = false;
                output.Advance(2.0);
                Assert.Equal(3.25, output.Position, 3); // past the old region end, on to the end of the sound
                Assert.True(output.IsPlaying);
                output.Advance(1.0);
                Assert.False(output.IsActive);
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task DraggingAnEdgeOrANewRegionWhileTheSoundPlaysChangesTheRangeLive()
    {
        await HeadlessApp.Run(async () =>
        {
            var output = AudioPlaybackTests.UseNullDevice();
            var rig = Open(4);
            try
            {
                rig.Start.Value = 1.0m;
                rig.End.Value = 3.0m;
                await rig.Transport.PlayPauseAsync();
                output.Advance(0.5); // at 1.5
                rig.Down(0.75, 2); // the end edge at 3.0
                rig.Move(0.5); // dragged to 2.0
                Assert.Equal((1.0, 2.0), rig.Region);
                Assert.Equal(1.5, output.Position, 3); // inside: no jump while dragging
                output.Advance(0.75); // 2.25 is past the new end
                Assert.False(output.IsActive);
                rig.Up(0.5);

                await rig.Transport.PlayPauseAsync();
                output.Advance(0.3);
                var before = output.Position;
                rig.Down(0.9);
                rig.Move(0.95); // a new region to the right of the playhead: the position is outside it, so it jumps there
                Assert.True(output.IsPlaying);
                Assert.Equal(rig.Region.Start, output.Position, 2);
                Assert.NotEqual(before, output.Position);
                rig.Up(0.95);
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task TheMiddleOfTheRegionMovesItShiftClickMovesTheNearerEdgeAndTheArrowKeysNudgeTheLastEdge()
    {
        await HeadlessApp.Run(() =>
        {
            AudioPlaybackTests.UseNullDevice();
            var rig = Open(4);
            try
            {
                rig.Start.Value = 1.0m;
                rig.End.Value = 2.0m;
                rig.Window.UpdateLayout();
                rig.Down(0.375); // the middle of the region (1.5 s): dragged 0.5 s to the right moves both ends
                rig.Move(0.5);
                rig.Up(0.5);
                Assert.Equal((1.5, 2.5), rig.Region);
                rig.Down(0.5);
                rig.Move(0.0); // far to the left: the region stops at the start of the sound and keeps its length
                rig.Up(0.0);
                Assert.Equal((0.0, 1.0), rig.Region);

                rig.Window.MouseDown(rig.At(0.6), MouseButton.Left, RawInputModifiers.Shift); // 2.4 s: nearer to the end (1.0) than to the start (0.0)
                rig.Up(0.6);
                Assert.Equal((0.0, 2.4), rig.Region);
                rig.Window.MouseDown(rig.At(0.1), MouseButton.Left, RawInputModifiers.Shift); // 0.4 s: nearer to the start
                rig.Up(0.1);
                Assert.Equal(0.4, rig.Region.Start, 1);
                Assert.Equal(2.4, rig.Region.End, 3);

                rig.Window.KeyPress(Key.Right, RawInputModifiers.None, PhysicalKey.ArrowRight, null); // the start was the last edge used
                Assert.Equal(0.41, rig.Region.Start, 2);
                rig.Window.KeyPress(Key.Left, RawInputModifiers.Shift, PhysicalKey.ArrowLeft, null);
                Assert.Equal(0.31, rig.Region.Start, 2);
                rig.Down(0.6, -1); // grab the end edge
                rig.Up(0.6, -1);
                rig.Window.KeyPress(Key.Left, RawInputModifiers.None, PhysicalKey.ArrowLeft, null);
                Assert.Equal(2.39, rig.Region.End, 2);
                Assert.Equal(0.31, rig.Region.Start, 2);

                rig.Window.MouseDown(rig.At(0.3), MouseButton.Left, RawInputModifiers.Control); // inside the region: Ctrl+drag selects a new one instead of moving it
                rig.Move(0.75);
                rig.Up(0.75);
                Assert.Equal((1.2, 3.0), rig.Region);
            }
            finally
            {
                rig.Window.Close();
                AudioDevices.Shutdown();
            }

            return Task.CompletedTask;
        });
    }

    [Theory]
    [InlineData(280)]
    [InlineData(360)]
    [InlineData(520)]
    public async Task TheToolbarWrapsAndNothingIsCutOffInRussianAtNarrowWidths(double width)
    {
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("ru");
            var view = new SoundPlayerView(AudioPlaybackTests.Sound(2), _ => true, () => new AudioOptions());
            var window = await LayoutAudit.Host(new ScrollViewer { Content = view }, width); // a wide font wraps the hint over more lines than the window is tall; the tab scrolls
            try
            {
                Assert.Empty(LayoutAudit.Overflows(window));
                var hint = AudioPlaybackTests.Find<TextBlock>(view, "AudioRegionHint");
                Assert.True(hint.Bounds.Width > 0 && hint.Bounds.Right <= width + 1, $"the hint is {hint.Bounds.Width:0} wide in {width}");
                Assert.True(hint.Bounds.Height > 20, "the long hint wraps onto several lines");
                var clear = AudioPlaybackTests.Find<Button>(view, "AudioClearRegion");
                Assert.True(clear.IsEffectivelyVisible && clear.Bounds.Right <= width + 1);
            }
            finally
            {
                window.Close();
                Loc.Current.SetLanguage("en");
            }
        });
    }

    [Fact]
    public async Task InTheAudioEditorTheRegionIsTheCutAndClearingItKeepsTheWholeSound()
    {
        await HeadlessApp.Run(() =>
        {
            AudioPlaybackTests.UseNullDevice();
            var window = new AudioEditorWindow("Edit audio", AudioCodec.Encode(new PcmAudio([.. Enumerable.Repeat(0.5f, 16000)], 1, 8000), "wav"), _ => { });
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            try
            {
                var root = (Control)window.Content!;
                var (start, end) = (AudioPlaybackTests.Find<NumericUpDown>(root, "AudioStart"), AudioPlaybackTests.Find<NumericUpDown>(root, "AudioEnd"));
                var wave = AudioPlaybackTests.Find<WaveformView>(root, "Waveform");
                var y = wave.TranslatePoint(default, window)!.Value.Y + 32;
                var x = wave.TranslatePoint(default, window)!.Value.X;
                Assert.Equal((0m, 2m), (start.Value, end.Value));

                window.MouseDown(new Point(x + wave.Bounds.Width * 0.25, y), MouseButton.Left);
                window.MouseMove(new Point(x + wave.Bounds.Width * 0.75, y));
                window.MouseUp(new Point(x + wave.Bounds.Width * 0.75, y), MouseButton.Left);
                Assert.Equal((0.5m, 1.5m), (start.Value, end.Value)); // the drag sets the cut

                window.MouseDown(new Point(x + wave.Bounds.Width * 0.5, y), MouseButton.Left); // the playhead is not the cut: a click leaves it
                window.MouseUp(new Point(x + wave.Bounds.Width * 0.5, y), MouseButton.Left);
                Assert.Equal((0.5m, 1.5m), (start.Value, end.Value));

                AudioPlaybackTests.Find<Button>(root, "AudioClearRegion").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal((0m, 2m), (start.Value, end.Value)); // cleared means the whole sound, not an empty cut
            }
            finally
            {
                window.Close();
                AudioDevices.Shutdown();
            }

            return Task.CompletedTask;
        });
    }
}
