using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using G3MStudio.Audio;
using G3MStudio.Core.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The sound recorder dialog driven with a fake microphone: the device list and the remembered choice, record, pause, stop, countdown, region, the messages of every failure, and the microphone going back.</summary>
public sealed class RecorderWindowTests
{
    private static readonly bool[] Both = [false, true];

    private static T Named<T>(Window root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static void Click(Window root, string name) => Named<Button>(root, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    private static string Message(Window root) => Named<TextBlock>(root, "RecordMessage").Text ?? "";

    internal static FakeCaptureSource Mics(params string[] names)
    {
        var source = new FakeCaptureSource { Default = names.FirstOrDefault() ?? "" };
        source.Names.AddRange(names);
        AudioCaptureDevices.Shutdown();
        AudioCaptureDevices.Source = source;
        return source;
    }

    private sealed class Rig(FakeCaptureSource source)
    {
        public RecorderWindow Dialog { get; set; } = null!;

        public FakeCaptureSource Source { get; } = source;

        public AudioOptions Options { get; set; } = new();

        public List<(string Name, byte[] Wav)> Created { get; } = [];

        public List<byte[]> Replaced { get; } = [];

        public FakeAudioCapture Mic => Source.Device ?? throw new InvalidOperationException("no microphone was opened");
    }

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000", Justification = "The dialog lives as long as the test.")]
    private static async Task<Rig> Open(FakeCaptureSource source, AudioOptions? options = null, bool create = true, bool replace = true, string? refuseName = null)
    {
        AudioPlaybackTests.UseNullDevice();
        var rig = new Rig(source) { Options = options ?? new() };
        string? Make(string name, byte[] wav)
        {
            rig.Created.Add((name, wav));
            return name == refuseName ? "That name is taken." : null;
        }

        Task<bool> Put(byte[] wav)
        {
            rig.Replaced.Add(wav);
            return Task.FromResult(true);
        }

        var dialog = new RecorderWindow(create ? Make : null, replace ? Put : null, replace ? "snd_target" : null, () => rig.Options, device => rig.Options = rig.Options with { CaptureDevice = device }, () => "snd_recorded");
        rig.Dialog = dialog;
        dialog.Show();
        await dialog.Listing;
        await LayoutAudit.Settle();
        return rig;
    }

    private static void End(Rig rig)
    {
        rig.Dialog.Close();
        AudioDevices.Shutdown();
    }

    private static async Task Record(Rig rig, double seconds, double amplitude = 0.5)
    {
        Click(rig.Dialog, "RecordStart");
        await AudioPlaybackTests.Until(() => rig.Dialog.IsRecording);
        if (seconds > 0) rig.Mic.Tone(seconds, amplitude);
    }

    private static async Task Stop(Rig rig)
    {
        Click(rig.Dialog, "RecordStop");
        await AudioPlaybackTests.Until(() => rig.Dialog.HasTake);
    }

    [Fact]
    public async Task TheListMarksTheDefaultMicrophoneAndTheChoiceIsRememberedOrFallsBackToTheDefault()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Desk Mic", "Headset"), new AudioOptions { CaptureDevice = "Headset" });
            try
            {
                var box = Named<ComboBox>(rig.Dialog, "RecordDevice");
                Assert.Equal([Loc.Current.Format("record.defaultMark", ("name", "Desk Mic")), "Headset"], box.Items.Cast<object>().Select(x => x.ToString()));
                Assert.Equal("Headset", rig.Dialog.ChosenName); // the remembered one
                box.SelectedIndex = 0; // the default one: remembered as "follow the system"
                Assert.Equal("", rig.Options.CaptureDevice);
                box.SelectedIndex = 1;
                Assert.Equal("Headset", rig.Options.CaptureDevice);
                rig.Dialog.Close();

                var missing = await Open(Mics("Desk Mic", "Headset"), new AudioOptions { CaptureDevice = "Unplugged" });
                Assert.Equal("Desk Mic", missing.Dialog.ChosenName); // not there: the default one
                Assert.Equal("Unplugged", missing.Options.CaptureDevice); // and not forgotten, so it is used again once it is back
                missing.Source.Names.Insert(0, "Unplugged");
                Click(missing.Dialog, "RecordRefresh");
                await AudioPlaybackTests.Until(() => missing.Dialog.ChosenName == "Unplugged");
                End(missing);
            }
            finally
            {
                AudioDevices.Shutdown();
            }
        });
    }

    [Fact]
    public async Task NothingIsRecordedUntilRecordIsPressedAndStopShowsTheTakeInThePlayer()
    {
        await HeadlessApp.Run(async () =>
        {
            var source = Mics("Mic");
            var rig = await Open(source);
            try
            {
                Assert.Null(source.Device); // the microphone is not even opened yet
                Assert.Empty(source.Opened);
                Assert.False(Named<Ellipse>(rig.Dialog, "RecordDot").IsVisible);
                Assert.False(rig.Dialog.Transport.IsEffectivelyVisible);
                Assert.False(Named<Button>(rig.Dialog, "RecordCreate").IsEnabled); // nothing to make a sound of yet, and the tip says so
                Assert.Equal(Loc.Current["record.takeFirst"], ToolTip.GetTip(Named<Button>(rig.Dialog, "RecordCreate")));
                Assert.False(Named<Button>(rig.Dialog, "RecordPause").IsEnabled);
                Assert.Equal(Loc.Current["record.pauseTip"], ToolTip.GetTip(Named<Button>(rig.Dialog, "RecordPause")));
                Assert.False(Named<Button>(rig.Dialog, "RecordStop").IsEnabled);

                await Record(rig, 0.5);
                Assert.True(Named<Ellipse>(rig.Dialog, "RecordDot").IsVisible); // the visible sign that it records
                Assert.True(rig.Mic.IsCapturing);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 22050);
                Assert.Equal(Loc.Current.Format("record.time", ("time", "0:00.5"), ("size", "0.0")), Named<TextBlock>(rig.Dialog, "RecordTime").Text); // the time and the size of the take so far, with no limit shown
                Assert.False(Named<Button>(rig.Dialog, "RecordStart").IsEnabled); // already recording, and the tip says so
                Assert.Equal(Loc.Current["record.startBusy"], ToolTip.GetTip(Named<Button>(rig.Dialog, "RecordStart")));
                Assert.False(Named<ComboBox>(rig.Dialog, "RecordDevice").IsEnabled);
                Assert.Equal(Loc.Current["record.locked"], ToolTip.GetTip(Named<ComboBox>(rig.Dialog, "RecordDevice")));

                await Stop(rig);
                Assert.False(Named<Ellipse>(rig.Dialog, "RecordDot").IsVisible);
                Assert.True(rig.Dialog.Transport.IsEffectivelyVisible);
                Assert.False(Named<AudioVisualizer>(rig.Dialog, "RecordMeter").IsVisible);
                Assert.Contains("0:00.5", rig.Dialog.Transport.TimeText, StringComparison.Ordinal); // the waveform and the length of the take
                Assert.Equal(Loc.Current["record.again"], Named<Button>(rig.Dialog, "RecordStart").Content);
                Assert.True(Named<Button>(rig.Dialog, "RecordCreate").IsEnabled);
                await AudioPlaybackTests.Until(() => rig.Mic.Disposed); // stopping gives the microphone back
                Assert.False(rig.Mic.IsCapturing);

                var output = AudioPlaybackTests.UseNullDevice();
                await rig.Dialog.Transport.PlayPauseAsync(); // and the player plays the take
                Assert.True(output.IsPlaying);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task PauseStopsTakingAndResumeContinuesTheSameTake()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                await Record(rig, 0.1);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 4410);
                Click(rig.Dialog, "RecordPause");
                await AudioPlaybackTests.Until(() => rig.Dialog.IsPaused);
                Assert.False(rig.Mic.IsCapturing);
                Assert.Equal(Loc.Current["record.resume"], Named<Button>(rig.Dialog, "RecordPause").Content);
                rig.Mic.Tone(0.3); // nothing arrives while paused
                await Task.Delay(100);
                Assert.Equal(4410, rig.Dialog.Take.Frames);
                Assert.False(Named<Ellipse>(rig.Dialog, "RecordDot").IsVisible);

                Click(rig.Dialog, "RecordPause");
                await AudioPlaybackTests.Until(() => rig.Dialog.IsRecording);
                Assert.Equal(2, rig.Mic.Starts);
                rig.Mic.Tone(0.2);
                await Stop(rig);
                Assert.Equal(4410 + 8820, rig.Dialog.Take.Frames); // the 0.1 s before the pause and the 0.2 s after it
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task TheCountdownDelaysTheStartAndStopDuringItRecordsNothing()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                rig.Dialog.CountdownSeconds = 2;
                rig.Dialog.CountdownStep = TimeSpan.FromMilliseconds(600); // the poll must catch the first number even when a slow runner delays it
                Named<CheckBox>(rig.Dialog, "RecordCountdown").IsChecked = true;
                Click(rig.Dialog, "RecordStart");
                await AudioPlaybackTests.Until(() => Named<TextBlock>(rig.Dialog, "RecordState").Text == Loc.Current.Format("record.stateCountdown", ("seconds", 2)));
                Assert.False(rig.Mic.IsCapturing); // counting: not recording
                Assert.False(rig.Dialog.IsRecording);
                Assert.True(Named<Button>(rig.Dialog, "RecordStop").IsEnabled);
                await AudioPlaybackTests.Until(() => rig.Dialog.IsRecording);
                Assert.True(rig.Mic.IsCapturing);

                rig.Mic.Tone(0.1);
                await Stop(rig);
                Assert.Equal(4410, rig.Dialog.Take.Frames);
            }
            finally
            {
                End(rig);
            }

            var second = await Open(Mics("Mic"));
            try
            {
                second.Dialog.CountdownStep = TimeSpan.FromMilliseconds(100);
                Named<CheckBox>(second.Dialog, "RecordCountdown").IsChecked = true;
                Click(second.Dialog, "RecordStart");
                await AudioPlaybackTests.Until(() => second.Source.Device is not null && Named<Button>(second.Dialog, "RecordStop").IsEnabled);
                Click(second.Dialog, "RecordStop"); // cancelled while counting
                await AudioPlaybackTests.Until(() => second.Mic.Disposed);
                await Task.Delay(450);
                Assert.Equal(0, second.Mic.Starts); // it never started
                Assert.False(second.Dialog.IsRecording);
                Assert.True(Named<Button>(second.Dialog, "RecordStart").IsEnabled);
            }
            finally
            {
                End(second);
            }
        });
    }

    [Fact]
    public async Task AnEmptyRecordingIsNoTakeAndSaysSo()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                await Record(rig, 0);
                Click(rig.Dialog, "RecordStop");
                await AudioPlaybackTests.Until(() => !rig.Dialog.IsRecording);
                Assert.False(rig.Dialog.HasTake);
                Assert.Equal(Loc.Current["record.empty"], Message(rig.Dialog));
                Assert.True(Named<Button>(rig.Dialog, "RecordStart").IsEnabled);
                Assert.False(Named<Button>(rig.Dialog, "RecordCreate").IsEnabled);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task TheRegionIsTheWholeTakeUntilOneIsDraggedAndThenOnlyThatPartIsMadeIntoASound()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                await Record(rig, 1.0);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 44100);
                await Stop(rig);
                await LayoutAudit.Settle();

                var wave = rig.Dialog.GetVisualDescendants().OfType<WaveformView>().Single();
                var at = wave.TranslatePoint(default, rig.Dialog)!.Value;
                double X(double fraction) => at.X + (wave.Bounds.Width * fraction);
                rig.Dialog.MouseDown(new Point(X(0.25), at.Y + 32), MouseButton.Left);
                rig.Dialog.MouseMove(new Point(X(0.75), at.Y + 32));
                rig.Dialog.MouseUp(new Point(X(0.75), at.Y + 32), MouseButton.Left);
                await LayoutAudit.Settle();
                Assert.Contains("[", rig.Dialog.Transport.TimeText, StringComparison.Ordinal); // the region shows in the time line
                Assert.Contains("0.50", Named<TextBlock>(rig.Dialog, "RecordInfo").Text, StringComparison.Ordinal);

                Click(rig.Dialog, "RecordCreate");
                var (name, wav) = Assert.Single(rig.Created);
                Assert.Equal("snd_recorded", name);
                var format = WavEdit.Read(wav);
                Assert.Equal((1, 44100, 16), (format.Channels, format.SampleRate, format.Bits));
                Assert.InRange(format.Seconds, 0.45, 0.55); // the half that was selected, not the whole second
                Assert.Equal(44100, rig.Dialog.Take.Frames);
                Assert.Equal(0, rig.Dialog.Take.Last(1).Frames); // the clip holds the audio now and the take's own copy is freed
                await AudioPlaybackTests.Until(() => !rig.Dialog.IsVisible);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task TheTakeHasTheWholePlayerWithRegionFieldsAndTheHintSaysOnlyTheRegionIsKept()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                var hint = Named<TextBlock>(rig.Dialog, "RecordSaveHint");
                Assert.False(hint.IsVisible); // no take yet
                await Record(rig, 0.5);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 22050);
                await Stop(rig);
                await LayoutAudit.Settle();
                Assert.True(hint.IsVisible);
                Assert.Equal(Loc.Current["audio.saveRegionHint"], hint.Text);
                Assert.All<string>(["RecordPlay", "AudioStop", "AudioPlayRegion", "AudioClearRegion", "AudioLoop", "ZoomIn", "ZoomOut", "ZoomFit", "AudioVisualizerShow", "AudioSeek", "RecordRegionStart", "RecordRegionEnd"], name => Assert.True(rig.Dialog.GetVisualDescendants().OfType<Control>().Any(c => c.Name == name && c.IsEffectivelyVisible), name));

                (Named<NumericUpDown>(rig.Dialog, "RecordRegionStart").Value, Named<NumericUpDown>(rig.Dialog, "RecordRegionEnd").Value) = (0.1m, 0.3m); // typed, not dragged
                Assert.Contains("0.20", Named<TextBlock>(rig.Dialog, "RecordInfo").Text, StringComparison.Ordinal);
                Click(rig.Dialog, "RecordCreate");
                Assert.Equal(8820, WavEdit.Read(Assert.Single(rig.Created).Wav).Frames);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task WithoutARegionTheWholeTakeIsKeptAndClearingTheRegionGoesBackToIt()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                await Record(rig, 0.4);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 17640);
                await Stop(rig);
                rig.Dialog.Transport.ClearRegion(); // nothing selected: still the whole take
                Click(rig.Dialog, "RecordCreate");
                Assert.Equal(17640, WavEdit.Read(Assert.Single(rig.Created).Wav).Frames);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task TheGainRaisesTheLevelAndAClippedTakeShowsTheWarningUntilTheNextRecording()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                var gain = Named<Slider>(rig.Dialog, "RecordGain");
                Assert.Equal(100, gain.Value);
                gain.Value = 300;
                Assert.Equal(3, rig.Dialog.Take.Gain);
                Assert.Equal("300%", Named<TextBlock>(rig.Dialog, "RecordGainText").Text);
                await Record(rig, 0.2, 0.5); // half scale times three: past the top
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 8820);
                Assert.True(rig.Dialog.Take.Clipped);
                Assert.True(Named<TextBlock>(rig.Dialog, "RecordClipping").IsVisible);
                Assert.Contains(Loc.Current["record.clipping"], Named<TextBlock>(rig.Dialog, "RecordClipping").Text, StringComparison.Ordinal);
                await Stop(rig);
                Assert.False(gain.IsEnabled); // the gain is for recording, and the tip says so
                Assert.Equal(Loc.Current["record.gainDone"], ToolTip.GetTip(gain));

                Click(rig.Dialog, "RecordStart"); // Record again asks before it drops the take
                await AudioPlaybackTests.Until(() => rig.Dialog.OwnedWindows.Count > 0);
                var ask = rig.Dialog.OwnedWindows[0];
                Click(ask, "KeepEditing");
                await AudioPlaybackTests.Until(() => !ask.IsVisible);
                await LayoutAudit.Settle();
                Assert.True(rig.Dialog.HasTake);
                Assert.Equal(8820, rig.Dialog.Take.Frames);

                Click(rig.Dialog, "RecordStart");
                await AudioPlaybackTests.Until(() => rig.Dialog.OwnedWindows.Count > 0);
                Click(rig.Dialog.OwnedWindows[0], "Discard");
                await AudioPlaybackTests.Until(() => rig.Dialog.IsRecording);
                Assert.Equal(0, rig.Dialog.Take.Frames);
                Assert.False(rig.Dialog.Take.Clipped);
                Assert.False(Named<TextBlock>(rig.Dialog, "RecordClipping").IsVisible);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task EveryFailureHasItsOwnMessageAndNothingStaysOpen()
    {
        await HeadlessApp.Run(async () =>
        {
            // No microphone at all: the message, and Record says why it cannot.
            var none = await Open(Mics());
            try
            {
                Assert.Equal(Loc.Current["record.failNoDevice"], Message(none.Dialog));
                Assert.False(Named<Button>(none.Dialog, "RecordStart").IsEnabled);
                Assert.Equal(Loc.Current["record.failNoDevice"], ToolTip.GetTip(Named<Button>(none.Dialog, "RecordStart")));
                Assert.False(Named<ComboBox>(none.Dialog, "RecordDevice").IsEnabled);
                Assert.True(Named<Button>(none.Dialog, "RecordRefresh").IsEnabled);
            }
            finally
            {
                End(none);
            }

            // The system blocks the microphone: said before anything is opened.
            var blocked = Mics("Mic");
            blocked.Problem = CaptureFailure.AccessDenied;
            var denied = await Open(blocked);
            try
            {
                Assert.Equal(Loc.Current["record.failDenied"], Message(denied.Dialog));
                Assert.True(Named<Button>(denied.Dialog, "RecordStart").IsEnabled); // the setting may be on since the list was read: Record tries, and says so when it is still off
                blocked.OpenFails = CaptureFailure.AccessDenied;
                Click(denied.Dialog, "RecordStart");
                await AudioPlaybackTests.Until(() => Named<Button>(denied.Dialog, "RecordStart").IsEnabled);
                Assert.Equal(Loc.Current["record.failDenied"], Message(denied.Dialog));
                blocked.OpenFails = null; // allowed now, and no Refresh was pressed
                blocked.Problem = null;
                await Record(denied, 0.1);
                Assert.Equal("", Message(denied.Dialog));
            }
            finally
            {
                End(denied);
            }

            // The library is missing.
            var library = Mics();
            library.Problem = CaptureFailure.LibraryMissing;
            var missing = await Open(library);
            try
            {
                Assert.Equal(Loc.Current.Format("record.failLibrary", ("file", AudioDevices.LibraryFile)), Message(missing.Dialog));
            }
            finally
            {
                End(missing);
            }

            // Opening fails when Record is pressed: one message each, and the dialog is ready for another try.
            foreach (var (failure, key) in new[] { (CaptureFailure.AccessDenied, "record.failDenied"), (CaptureFailure.Busy, "record.failBusy"), (CaptureFailure.NoDevice, "record.failNoDevice"), (CaptureFailure.LibraryMissing, "record.failLibrary") })
            {
                var source = Mics("Mic");
                var rig = await Open(source);
                try
                {
                    source.OpenFails = failure;
                    Click(rig.Dialog, "RecordStart");
                    await AudioPlaybackTests.Until(() => Message(rig.Dialog).Length > 0);
                    Assert.Equal(failure == CaptureFailure.LibraryMissing ? Loc.Current.Format(key, ("file", AudioDevices.LibraryFile)) : Loc.Current[key], Message(rig.Dialog));
                    Assert.False(rig.Dialog.IsRecording);
                    await AudioPlaybackTests.Until(() => Named<Button>(rig.Dialog, "RecordStart").IsEnabled);
                    source.OpenFails = null;
                    await Record(rig, 0.1); // and it works once the cause is gone
                    Assert.Equal("", Message(rig.Dialog));
                }
                finally
                {
                    End(rig);
                }
            }

            // The microphone cannot be started.
            var startSource = Mics("Mic");
            var starting = await Open(startSource);
            try
            {
                await Record(starting, 0.1);
                await AudioPlaybackTests.Until(() => starting.Dialog.Take.Frames == 4410);
                Click(starting.Dialog, "RecordPause");
                await AudioPlaybackTests.Until(() => starting.Dialog.IsPaused);
                starting.Mic.Fail(CaptureFailure.Busy);
                Click(starting.Dialog, "RecordPause"); // resume is refused: what was taken before the pause is kept
                await AudioPlaybackTests.Until(() => starting.Dialog.HasTake);
                Assert.Equal(Loc.Current["record.failBusy"], Message(starting.Dialog));
                Assert.False(starting.Dialog.IsRecording);
                Assert.Equal(4410, starting.Dialog.Take.Frames);
                Assert.True(Named<Button>(starting.Dialog, "RecordCreate").IsEnabled);
                await AudioPlaybackTests.Until(() => starting.Mic.Disposed);
            }
            finally
            {
                End(starting);
            }
        });
    }

    [Fact]
    public async Task AMicrophoneLostWhileRecordingKeepsWhatWasRecordedAndSaysSo()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                await Record(rig, 0.3);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 13230);
                rig.Mic.Fail(CaptureFailure.Lost); // unplugged
                await AudioPlaybackTests.Until(() => rig.Dialog.HasTake);
                Assert.Equal(Loc.Current["record.failLost"], Message(rig.Dialog));
                Assert.Equal(13230, rig.Dialog.Take.Frames);
                Assert.True(Named<Button>(rig.Dialog, "RecordCreate").IsEnabled); // the part that was recorded can still be used
                await AudioPlaybackTests.Until(() => rig.Mic.Disposed);
            }
            finally
            {
                End(rig);
            }

            var empty = await Open(Mics("Mic"));
            try
            {
                await Record(empty, 0);
                empty.Mic.Fail(CaptureFailure.Lost); // lost before anything came
                await AudioPlaybackTests.Until(() => Message(empty.Dialog).Length > 0);
                Assert.Equal(Loc.Current["record.failLost"], Message(empty.Dialog));
                Assert.False(empty.Dialog.HasTake);
                Assert.False(empty.Dialog.IsRecording);
            }
            finally
            {
                End(empty);
            }
        });
    }

    [Fact]
    public async Task StopDuringAResumeLeavesTheStoppedTakeAlone()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            try
            {
                await Record(rig, 0.1);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 4410);
                Click(rig.Dialog, "RecordPause");
                await AudioPlaybackTests.Until(() => rig.Dialog.IsPaused);
                rig.Mic.OnStarted = () => { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); }; // a slow driver
                Click(rig.Dialog, "RecordPause"); // resume, held inside the start
                await AudioPlaybackTests.Until(() => entered.IsSet);
                Click(rig.Dialog, "RecordStop");
                Assert.True(rig.Dialog.HasTake);
                release.Set(); // the start finishes after Stop has taken the microphone back
                await AudioPlaybackTests.Until(() => Named<Button>(rig.Dialog, "RecordStart").IsEnabled); // the resume is over
                Assert.True(rig.Dialog.HasTake); // and it did not put the dialog back to Recording
                Assert.False(rig.Dialog.IsRecording);
                Assert.Equal(4410, rig.Dialog.Take.Frames);
                Assert.True(Named<Button>(rig.Dialog, "RecordCreate").IsEnabled);
            }
            finally
            {
                release.Set();
                End(rig);
            }
        });
    }

    [Fact]
    public async Task WhatArrivesUpToTheStopIsKeptOnPauseAndOnStop()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                await Record(rig, 0.1);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 4410);
                rig.Mic.OnStopping = () => rig.Mic.Feed(new short[441]); // arrives between the last read and the stop
                Click(rig.Dialog, "RecordPause");
                await AudioPlaybackTests.Until(() => rig.Dialog.IsPaused);
                Assert.Equal(4410 + 441, rig.Dialog.Take.Frames);

                rig.Mic.OnStopping = null;
                Click(rig.Dialog, "RecordPause");
                await AudioPlaybackTests.Until(() => rig.Dialog.IsRecording);
                rig.Mic.OnStopping = () => rig.Mic.Feed(new short[220]);
                await Stop(rig);
                Assert.Equal(4410 + 441 + 220, rig.Dialog.Take.Frames);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task AMicrophoneTakenByAnotherOpenEndsTheRecordingAsLostAndKeepsTheTake()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                await Record(rig, 0.3);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 13230);
                AudioCaptureDevices.Open(""); // opening another device closes this one, which must not read as silence
                await AudioPlaybackTests.Until(() => rig.Dialog.HasTake);
                Assert.Equal(Loc.Current["record.failLost"], Message(rig.Dialog));
                Assert.Equal(13230, rig.Dialog.Take.Frames);
            }
            finally
            {
                AudioCaptureDevices.Shutdown();
                End(rig);
            }
        });
    }

    [Fact]
    public async Task RecordAgainWithoutAMicrophoneKeepsTheOldTake()
    {
        await HeadlessApp.Run(async () =>
        {
            var source = Mics("Mic");
            var rig = await Open(source);
            try
            {
                await Record(rig, 0.2);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 8820);
                await Stop(rig);
                source.OpenFails = CaptureFailure.NoDevice; // unplugged since
                Click(rig.Dialog, "RecordStart");
                await AudioPlaybackTests.Until(() => rig.Dialog.OwnedWindows.Count > 0);
                Click(rig.Dialog.OwnedWindows[0], "Discard");
                await AudioPlaybackTests.Until(() => Message(rig.Dialog) == Loc.Current["record.failNoDevice"]);
                Assert.True(rig.Dialog.HasTake); // nothing took its place, so it is still there
                Assert.Equal(8820, rig.Dialog.Take.Frames);
                Assert.True(rig.Dialog.Transport.IsVisible);
                Assert.True(Named<Button>(rig.Dialog, "RecordCreate").IsEnabled);
                Assert.True(Named<Button>(rig.Dialog, "RecordStart").IsEnabled);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task RecordPressedWhileTheListIsReadUsesTheRememberedMicrophone()
    {
        await HeadlessApp.Run(async () =>
        {
            var source = Mics("Desk Mic");
            var rig = await Open(source, new AudioOptions { CaptureDevice = "Headset" });
            using var entered = new ManualResetEventSlim();
            using var release = new ManualResetEventSlim();
            try
            {
                source.Names.Add("Headset"); // plugged in since
                source.OnList = () => { entered.Set(); release.Wait(TimeSpan.FromSeconds(10)); };
                Click(rig.Dialog, "RecordRefresh");
                await AudioPlaybackTests.Until(() => entered.IsSet);
                Click(rig.Dialog, "RecordStart"); // the list is not read yet: the choice is still "Desk Mic"
                release.Set();
                await AudioPlaybackTests.Until(() => rig.Dialog.IsRecording);
                Assert.Equal(["Headset"], source.Opened);
            }
            finally
            {
                release.Set();
                End(rig);
            }
        });
    }

    [Fact]
    public async Task ClosingWhileTheMicrophoneOpensOrCountsDownJustCancelsAndOnlyATakeIsAskedAbout()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                rig.Dialog.CountdownStep = TimeSpan.FromMilliseconds(100);
                Named<CheckBox>(rig.Dialog, "RecordCountdown").IsChecked = true;
                Click(rig.Dialog, "RecordStart");
                await AudioPlaybackTests.Until(() => rig.Source.Device is not null && Named<Button>(rig.Dialog, "RecordStop").IsEnabled);
                Click(rig.Dialog, "RecordCancel"); // nothing is recorded yet: no question, and the countdown ends with the window
                await AudioPlaybackTests.Until(() => !rig.Dialog.IsVisible);
                Assert.Empty(rig.Dialog.OwnedWindows);
                await AudioPlaybackTests.Until(() => rig.Mic.Disposed);
                await Task.Delay(450);
                Assert.Equal(0, rig.Mic.Starts);
            }
            finally
            {
                End(rig);
            }

            var second = await Open(Mics("Mic"));
            try
            {
                await Record(second, 0.2);
                await AudioPlaybackTests.Until(() => second.Dialog.Take.Frames == 8820);
                Click(second.Dialog, "RecordCancel"); // there is a take now: it asks, and the recording goes on behind the question
                await AudioPlaybackTests.Until(() => second.Dialog.OwnedWindows.Count > 0);
                Click(second.Dialog.OwnedWindows[0], "KeepEditing");
                await LayoutAudit.Settle();
                Assert.True(second.Dialog.IsVisible);
                Assert.True(second.Dialog.IsRecording);
            }
            finally
            {
                End(second);
            }
        });
    }

    [Fact]
    public async Task ARefreshWithATakeOnShowKeepsItsNote()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                await Record(rig, 0.2);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 8820);
                rig.Mic.Fail(CaptureFailure.Lost);
                await AudioPlaybackTests.Until(() => rig.Dialog.HasTake);
                Assert.Equal(Loc.Current["record.failLost"], Message(rig.Dialog));
                Click(rig.Dialog, "RecordRefresh");
                await rig.Dialog.Listing;
                Assert.Equal(Loc.Current["record.failLost"], Message(rig.Dialog)); // the list has no say over the note of the take
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task RecordingStartsBySilencingWhatTheProgramPlays()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            var taken = 0;
            try
            {
                Assert.NotNull(AudioDevices.Acquire(rig.Options, new object(), () => taken++)); // some player holds the output
                Click(rig.Dialog, "RecordStart");
                await AudioPlaybackTests.Until(() => rig.Dialog.IsRecording);
                Assert.Equal(1, taken);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task TheLimitStopsTheRecordingWithAMessageAndKeepsTheTake()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                rig.Dialog.Take.LimitFrames = 8820;
                await Record(rig, 0.5);
                await AudioPlaybackTests.Until(() => rig.Dialog.HasTake);
                Assert.Equal(8820, rig.Dialog.Take.Frames);
                Assert.Equal(Loc.Current["record.capReached"], Message(rig.Dialog));
                await AudioPlaybackTests.Until(() => rig.Mic.Disposed);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task ARecordingHasNoTimeLimitAndShowsTheSizeOfTheTake()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                await Record(rig, 12 * 60, 0.1); // longer than the old limit of ten minutes
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 12 * 60 * 44100);
                Assert.True(rig.Dialog.IsRecording); // still going
                Assert.Equal(Loc.Current.Format("record.time", ("time", "12:00.0"), ("size", (12 * 60 * 44100 * 2 / 1048576.0).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture))), Named<TextBlock>(rig.Dialog, "RecordTime").Text);
                await Stop(rig);
                Assert.Equal("", Message(rig.Dialog));
                Assert.Contains("12:00.0", rig.Dialog.Transport.TimeText, StringComparison.Ordinal);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task RunningOutOfMemoryStopsTheRecordingKeepsTheTakeAndTellsTheOwner()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                var asked = new List<long>();
                var told = 0;
                rig.Dialog.HasMemoryFor = bytes =>
                {
                    asked.Add(bytes);
                    return bytes <= 4 * 8000; // room for 8000 frames of the clip
                };
                rig.Dialog.OutOfMemory = () => told++;
                await Record(rig, 1.0);
                await AudioPlaybackTests.Until(() => rig.Dialog.HasTake);
                Assert.Equal(44100, rig.Dialog.Take.Frames);
                Assert.Equal(Loc.Current["record.memoryStop"], Message(rig.Dialog));
                Assert.Equal(1, told);
                Assert.Equal(4L * 44100, asked[^1]); // what the clip of the take needs: a float for each frame
                Assert.Contains("0:01.0", rig.Dialog.Transport.TimeText, StringComparison.Ordinal); // the take is there to use
                Assert.True(Named<Button>(rig.Dialog, "RecordCreate").IsEnabled);
                await AudioPlaybackTests.Until(() => rig.Mic.Disposed);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task ClosingWhileRecordingGivesTheMicrophoneBackAtOnce()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            await Record(rig, 0.2);
            var mic = rig.Mic;
            Assert.True(mic.IsCapturing);
            rig.Dialog.Close();
            await AudioPlaybackTests.Until(() => mic.Disposed);
            Assert.False(mic.IsCapturing);

            var paused = await Open(Mics("Mic")); // and paused
            await Record(paused, 0.1);
            Click(paused.Dialog, "RecordPause");
            await AudioPlaybackTests.Until(() => paused.Dialog.IsPaused);
            var other = paused.Mic;
            paused.Dialog.Close();
            await AudioPlaybackTests.Until(() => other.Disposed);

            var last = await Open(Mics("Mic")); // and the program ending
            await Record(last, 0.1);
            var third = last.Mic;
            AudioCaptureDevices.Shutdown();
            Assert.True(third.Disposed);
            last.Dialog.Close();
            AudioDevices.Shutdown();
        });
    }

    [Fact]
    public async Task ClosingWithATakeAsksFirstAndAnUnusedTakeIsOnlyDiscardedOnAnswer()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"));
            try
            {
                Click(rig.Dialog, "RecordCancel"); // nothing recorded: closes at once
                await AudioPlaybackTests.Until(() => !rig.Dialog.IsVisible);
            }
            finally
            {
                End(rig);
            }

            var second = await Open(Mics("Mic"));
            try
            {
                await Record(second, 0.2);
                await AudioPlaybackTests.Until(() => second.Dialog.Take.Frames == 8820);
                await Stop(second);
                Click(second.Dialog, "RecordCancel"); // a take that is not in a sound: it asks
                await AudioPlaybackTests.Until(() => second.Dialog.OwnedWindows.Count > 0);
                var ask = second.Dialog.OwnedWindows[0];
                Assert.Contains(Loc.Current["record.discard"], ask.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToList());
                Click(ask, "KeepEditing");
                await AudioPlaybackTests.Until(() => !ask.IsVisible);
                await LayoutAudit.Settle(); // the question is over only when its continuation ran
                Assert.True(second.Dialog.IsVisible);
                Assert.True(second.Dialog.HasTake);

                Click(second.Dialog, "RecordCancel");
                await AudioPlaybackTests.Until(() => second.Dialog.OwnedWindows.Count > 0);
                Click(second.Dialog.OwnedWindows[0], "Discard");
                await AudioPlaybackTests.Until(() => !second.Dialog.IsVisible);
                Assert.Empty(second.Created);
            }
            finally
            {
                End(second);
            }
        });
    }

    [Fact]
    public async Task CreateMakesOneSoundHoldsTheNameRulesAndAClickOnTheClosedDialogDoesNothing()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"), refuseName: "snd_taken");
            try
            {
                await Record(rig, 0.2);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 8820);
                await Stop(rig);
                var name = Named<TextBox>(rig.Dialog, "RecordName");
                name.Text = " ";
                Click(rig.Dialog, "RecordCreate");
                Assert.Equal(Loc.Current["synth.noName"], Message(rig.Dialog));
                Assert.Empty(rig.Created);
                name.Text = "snd_taken";
                Click(rig.Dialog, "RecordCreate");
                Assert.Equal("That name is taken.", Message(rig.Dialog)); // the reason stays in the dialog, the take stays too
                Assert.True(rig.Dialog.HasTake);
                name.Text = "snd_voice";
                Click(rig.Dialog, "RecordCreate");
                Click(rig.Dialog, "RecordCreate"); // a double click while the dialog fades out
                Assert.Equal(["snd_taken", "snd_voice"], rig.Created.Select(c => c.Name));
                await AudioPlaybackTests.Until(() => !rig.Dialog.IsVisible);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task ReplaceStartsOneEditWhileItWorksAndWorksAgainAfterARefusal()
    {
        await HeadlessApp.Run(async () =>
        {
            AudioPlaybackTests.UseNullDevice();
            var source = Mics("Mic");
            var results = new List<TaskCompletionSource<bool>>();
            var options = new AudioOptions();
            var dialog = new RecorderWindow(null, _ =>
            {
                var result = new TaskCompletionSource<bool>();
                results.Add(result);
                return result.Task;
            }, "snd_target", () => options, _ => { });
            dialog.Show();
            await dialog.Listing;
            var rig = new Rig(source) { Dialog = dialog };
            try
            {
                await Record(rig, 0.2);
                await AudioPlaybackTests.Until(() => dialog.Take.Frames == 8820);
                await Stop(rig);
                Assert.True(Named<Button>(dialog, "RecordReplace").IsDefault); // no name field: Replace is the main action
                Click(dialog, "RecordReplace");
                Click(dialog, "RecordReplace"); // a second click while the first edit works
                Assert.Single(results);
                results[0].SetResult(false); // refused: the dialog stays and the button works again
                await AudioPlaybackTests.Until(() => { Click(dialog, "RecordReplace"); return results.Count == 2; });
                results[1].SetResult(true);
                await AudioPlaybackTests.Until(() => !dialog.IsVisible);
                Assert.Equal(2, results.Count);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task ReplaceWithoutASelectedSoundIsOffAndSaysWhy()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"), replace: false);
            try
            {
                await Record(rig, 0.1);
                await AudioPlaybackTests.Until(() => rig.Dialog.Take.Frames == 4410);
                await Stop(rig);
                var put = Named<Button>(rig.Dialog, "RecordReplace");
                Assert.False(put.IsEnabled);
                Assert.Equal(Loc.Current["sounds.noSound"], ToolTip.GetTip(put));
                Assert.True(Named<Button>(rig.Dialog, "RecordCreate").IsEnabled);
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Fact]
    public async Task OnlyReplaceHasNoNameFieldWhenTheRecorderOpensFromTheAudioEditor()
    {
        await HeadlessApp.Run(async () =>
        {
            var rig = await Open(Mics("Mic"), create: false);
            try
            {
                Assert.DoesNotContain(rig.Dialog.GetVisualDescendants().OfType<TextBox>(), t => t.Name == "RecordName");
                Assert.DoesNotContain(rig.Dialog.GetVisualDescendants().OfType<Button>(), t => t.Name == "RecordCreate");
            }
            finally
            {
                End(rig);
            }
        });
    }

    [Theory]
    [InlineData(100)]
    [InlineData(125)]
    [InlineData(150)]
    public async Task TheDialogFitsAtEveryScaleInEveryLanguageBeforeAndAfterARecordingAtItsOwnSizeAndSqueezed(int scale)
    {
        using var dir = new TempDir();
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var owner = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                owner.Workspaces.Settings.Update(x => x with { UiScale = scale });
                foreach (var language in LanguageLayoutAuditTests.Languages.Select(row => row.Data))
                {
                    Loc.Current.SetLanguage(language);
                    foreach (var recorded in Both)
                    {
                        var source = Mics("A microphone with a long name " + language, "Headset");
                        AudioPlaybackTests.UseNullDevice();
                        var dialog = new RecorderWindow((_, _) => null, _ => Task.FromResult(true), "a_very_long_sound_name_" + language, () => new AudioOptions(), _ => { });
                        dialog.Show(owner);
                        await dialog.Listing;
                        if (recorded)
                        {
                            Click(dialog, "RecordStart");
                            await AudioPlaybackTests.Until(() => dialog.IsRecording);
                            source.Device!.Tone(0.5);
                            await AudioPlaybackTests.Until(() => dialog.Take.Frames == 22050);
                            Named<Slider>(dialog, "RecordGain").Value = 400; // the clipping line is part of the layout too
                            source.Device.Feed(short.MaxValue);
                            await AudioPlaybackTests.Until(() => dialog.Take.Clipped);
                            Click(dialog, "RecordStop");
                            await AudioPlaybackTests.Until(() => dialog.HasTake);
                        }

                        await LayoutAudit.Settle();
                        var tag = $"{scale}% {language} {(recorded ? "take" : "ready")}";
                        problems.AddRange(DialogLayoutTests.Audit(dialog).Concat(LayoutAudit.CutText(dialog)).Select(p => $"{tag}: {p}"));
                        dialog.MaxHeight = Math.Max(dialog.MinHeight, 220 * scale / 100.0); // the smallest a window that sizes itself can get
                        await LayoutAudit.Settle();
                        problems.AddRange(DialogLayoutTests.Audit(dialog).Concat(LayoutAudit.CutText(dialog)).Select(p => $"{tag} (smallest): {p}"));
                        dialog.Close();
                        await LayoutAudit.Settle();
                    }
                }
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                owner.Close();
                AudioDevices.Shutdown();
                AudioCaptureDevices.Shutdown();
            }
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }
}
