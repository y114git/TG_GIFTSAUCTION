using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using G3MStudio.Audio;
using G3MStudio.Core.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>
/// The sound recorder: pick a microphone, press Record (nothing is taken before that), watch the level, pause and stop; the take then shows in the player and waveform of the audio editor, where a
/// region marks the part to keep, and becomes a new sound or the audio of the sound selected. It follows <see cref="SynthWindow"/>: the owner does the edit and a callback returns the reason a name
/// was refused, which stays in the dialog. The samples live in memory only until a sound is made of them; closing with a take that was not used asks first, and the microphone is always given back.
/// The device is opened, started and closed off the UI thread; a timer reads what arrived, which never blocks.
/// </summary>
public sealed partial class RecorderWindow : Window
{
    private enum Phase { Idle, Opening, Countdown, Recording, Paused, Stopped }

    private readonly ComboBox _device = new() { Name = "RecordDevice", MinWidth = 200, MaxWidth = 360 };
    private readonly Button _refresh = new() { Name = "RecordRefresh" };
    private readonly Slider _gain = new() { Name = "RecordGain", Minimum = 0, Maximum = RecordingTake.MaxGain * 100, Value = 100, SmallChange = 5, LargeChange = 25, MinWidth = 160, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _gainText = new() { Name = "RecordGainText", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _clipped = new() { Name = "RecordClipping", TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly AudioVisualizer _meter = new() { Name = "RecordMeter" };
    private readonly Ellipse _dot = new() { Name = "RecordDot", Width = 12, Height = 12, VerticalAlignment = VerticalAlignment.Center, IsVisible = false };
    private readonly TextBlock _state = new() { Name = "RecordState", TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _time = new() { Name = "RecordTime", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
    private readonly Button _record, _pause, _stop;
    private readonly CheckBox _countdown = new() { Name = "RecordCountdown" };
    private readonly TextBlock _info = new() { Name = "RecordInfo", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private readonly TextBox _name = new() { Name = "RecordName" };
    private readonly TextBlock _message = new() { Name = "RecordMessage", TextWrapping = TextWrapping.Wrap };
    private Button? _create;
    private Button? _put;
    private readonly DispatcherTimer _pump = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly Func<AudioOptions> _audio;
    private readonly bool _canReplace;
    private readonly Action<string> _remember;
    private readonly short[] _block = new short[IAudioCapture.SampleRate / 10];
    private List<string> _names = [];
    private string _default = "";
    private string? _problem; // why Record cannot start now (no microphone, blocked, no library), shown in the tip of the button
    private IAudioCapture? _capture;
    private PcmAudio _clip = new([], 1, RecordingTake.SampleRate);
    private readonly RegionPlayer _player;
    private readonly TextBlock _saveHint = new() { Name = "RecordSaveHint", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Hint, TextWrapping = TextWrapping.Wrap, IsVisible = false };
    private Phase _phase;
    private bool _building, _working, _listing, _closed, _done;

    /// <param name="create">Makes a new sound of the name and the WAV and returns why it was refused, or null; null hides the name field and the button.</param>
    /// <param name="replace">Puts the WAV into the sound and returns whether that was done; null turns the button off.</param>
    /// <param name="replaceName">The name of the sound the audio would replace, as it is shown to the user.</param>
    /// <param name="audio">The playback options of the player; they also hold the microphone chosen last.</param>
    /// <param name="remember">Stores the microphone the user chose (empty is the system default).</param>
    /// <param name="suggestName">A free name for a new sound.</param>
    public RecorderWindow(Func<string, byte[], string?>? create, Func<byte[], Task<bool>>? replace, string? replaceName, Func<AudioOptions> audio, Action<string> remember, Func<string>? suggestName = null)
    {
        (_audio, _remember) = (audio, remember);
        _canReplace = replace is not null && replaceName is not null;
        Title = Loc.Current["record.title"];
        Width = 560;
        MinWidth = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _name.Text = suggestName?.Invoke() ?? "snd_recorded";
        _dot.Fill = (IBrush?)Application.Current?.FindResource("Danger");
        _clipped.Foreground = (IBrush?)Application.Current?.FindResource("Danger");
        _clipped.Text = Loc.Current["record.clipping"];
        _refresh.Content = Loc.Current["record.refresh"];
        ToolTip.SetShowOnDisabled(_refresh, true);
        _refresh.Click += (_, _) => { if (!_listing) Listing = RefreshAsync(); };
        _device.ItemTemplate = new Avalonia.Controls.Templates.FuncDataTemplate<string>((text, _) => new TextBlock { Text = text, TextTrimming = TextTrimming.CharacterEllipsis }); // a long driver name is cut with an ellipsis, whole in the list
        _device.SelectionChanged += (_, _) => Remember();
        ToolTip.SetShowOnDisabled(_device, true);
        Avalonia.Automation.AutomationProperties.SetName(_device, Loc.Current["record.device"]);
        Avalonia.Automation.AutomationProperties.SetName(_gain, Loc.Current["record.gain"]);
        _countdown.Content = Loc.Current["record.countdown"];
        ToolTip.SetShowOnDisabled(_countdown, true);
        ToolTip.SetTip(_gain, Loc.Current["record.gainTip"]);
        ToolTip.SetShowOnDisabled(_gain, true);
        _gain.PropertyChanged += (_, e) =>
        {
            if (e.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty) return;
            Take.Gain = _gain.Value / 100;
            _gainText.Text = Math.Round(_gain.Value).ToString(CultureInfo.InvariantCulture) + "%";
        };
        _gainText.Text = "100%";
        _record = new Button { Name = "RecordStart" };
        _pause = new Button { Name = "RecordPause" };
        _stop = new Button { Name = "RecordStop", Content = Loc.Current["audio.stop"] };
        foreach (var button in new[] { _record, _pause, _stop }) ToolTip.SetShowOnDisabled(button, true);
        _record.Click += (_, _) => _ = RecordAsync();
        _pause.Click += (_, _) => _ = PauseOrResumeAsync();
        _stop.Click += (_, _) => End(null);
        _player = new RegionPlayer(0.01, () => _clip, () => audio() with { Visualizer = false }, "RecordPlay", "RecordRegion") { IsVisible = false };
        Transport.NoDevice = () => _message.Text = Loc.Current["audio.noDevice"];
        _player.RegionChanged += ShowInfo; // cleared: the whole take
        _saveHint.Text = Loc.Current["audio.saveRegionHint"];
        _pump.Tick += (_, _) => Tick();

        var rows = new StackPanel { Spacing = 8 };
        rows.Children.Add(DialogFrame.Form(
            (Loc.Current["record.device"], new WrapPanel { ItemSpacing = 8, LineSpacing = 6, Children = { _device, _refresh } }),
            (Loc.Current["record.gain"], new WrapPanel { ItemSpacing = 8, LineSpacing = 6, Children = { _gain, _gainText } })));
        rows.Children.Add(new WrapPanel { ItemSpacing = 8, LineSpacing = 4, Children = { _dot, _state, _time } });
        rows.Children.Add(_meter);
        rows.Children.Add(_clipped);
        rows.Children.Add(_player);
        rows.Children.Add(_info);
        rows.Children.Add(new WrapPanel { ItemSpacing = 8, LineSpacing = 6, Children = { _record, _pause, _stop, _countdown } });
        if (create is not null) rows.Children.Add(DialogFrame.Form((Loc.Current["synth.name"], _name)));

        var actions = Actions(create, replace, replaceName);
        Closed += (_, _) =>
        {
            _closed = true;
            _pump.Stop();
            Transport.Close();
            ReleaseCapture();
        };
        DialogFrame.DiscardMessage(this, Loc.Current["record.discard"]);
        DialogFrame.Show(this, rows, actions, new StackPanel { Spacing = 4, Children = { _saveHint, _message } }, DialogScroll.Vertical, () => _phase == Phase.Stopped || (_phase is Phase.Recording or Phase.Paused && Take.Frames > 0)); // only a take can be lost: closing while the microphone opens or counts down just cancels
        Update();
        Listing = RefreshAsync();
    }

    private List<Control> Actions(Func<string, byte[], string?>? create, Func<byte[], Task<bool>>? replace, string? replaceName)
    {
        var actions = new List<Control>();
        if (create is not null)
        {
            _create = DialogFrame.Action(this, Loc.Current["synth.create"], "RecordCreate", () =>
            {
                var name = (_name.Text ?? "").Trim();
                if (_done || _working) return;
                if (name.Length == 0) _message.Text = Loc.Current["synth.noName"];
                else if (create(name, Wav()) is { } refused) _message.Text = refused;
                else (_done, _phase) = (true, Phase.Idle); // the take is in a sound now: closing loses nothing
                if (_done) Close(true);
            }, DialogRole.Primary);
            ToolTip.SetShowOnDisabled(_create, true);
            actions.Add(_create);
        }

        _put = DialogFrame.Action(this, replaceName is null ? Loc.Current["synth.replaceNone"] : Loc.Current.Format("synth.replace", ("sound", replaceName)), "RecordReplace", () => _ = ReplaceAsync(replace), create is null ? DialogRole.Primary : DialogRole.Normal);
        ToolTip.SetShowOnDisabled(_put, true);
        actions.Add(_put);
        actions.Add(DialogFrame.Cancel(this, () => Close(false), "RecordCancel"));
        return actions;
    }

    /// <summary>Plays the take and holds its region; shown once a recording is stopped.</summary>
    public AudioTransport Transport => _player.Transport;

    /// <summary>The microphone list has been read (a refresh in progress), for tests.</summary>
    internal Task Listing { get; private set; }

    /// <summary>The seconds of the countdown before the recording starts, and the time one second of it takes; tests shorten both.</summary>
    internal int CountdownSeconds { get; set; } = 3;

    internal TimeSpan CountdownStep { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>The take, for tests.</summary>
    internal RecordingTake Take { get; } = new();

    /// <summary>Says whether the memory has room for this many more bytes; the recorder stops, keeping the take, when it does not. The take is not limited in time, only by the memory. Null means no check.</summary>
    internal Func<long, bool>? HasMemoryFor { get; set; }

    /// <summary>Runs when the recording stopped because the memory ran short, so the owner frees what it can and tells the user.</summary>
    internal Action? OutOfMemory { get; set; }

    // The system blocks the microphone, as the last list said.
    private bool Denied { get; set; }

    internal bool IsRecording => _phase == Phase.Recording;

    internal bool IsPaused => _phase == Phase.Paused;

    internal bool HasTake => _phase == Phase.Stopped;

    /// <summary>The microphone the list shows as chosen: empty for the system default.</summary>
    internal string ChosenName => _device.SelectedIndex >= 0 && _device.SelectedIndex < _names.Count ? _names[_device.SelectedIndex] : "";

    // The state of every control. A button that cannot act says why in its tip.
    private void Update()
    {
        var phase = _phase;
        var idle = phase is Phase.Idle or Phase.Stopped;
        _state.Text = phase switch
        {
            Phase.Opening => Loc.Current["record.stateOpening"],
            Phase.Recording => Loc.Current["record.stateRecording"],
            Phase.Paused => Loc.Current["record.statePaused"],
            Phase.Stopped => Loc.Current["record.stateDone"],
            Phase.Countdown => _state.Text,
            _ => Loc.Current["record.stateIdle"],
        };
        _dot.IsVisible = phase == Phase.Recording;
        _record.Content = Loc.Current[phase == Phase.Stopped ? "record.again" : "record.start"];
        _record.IsEnabled = idle && !_working && (phase == Phase.Stopped || !Blocked);
        ToolTip.SetTip(_record, _record.IsEnabled ? null : idle && _problem is not null ? _problem : Loc.Current["record.startBusy"]);
        _pause.Content = Loc.Current[phase == Phase.Paused ? "record.resume" : "audio.pause"];
        _pause.IsEnabled = phase is Phase.Recording or Phase.Paused && !_working;
        ToolTip.SetTip(_pause, _pause.IsEnabled ? null : Loc.Current["record.pauseTip"]);
        _stop.IsEnabled = phase is Phase.Countdown or Phase.Recording or Phase.Paused;
        ToolTip.SetTip(_stop, _stop.IsEnabled ? null : Loc.Current["record.stopTip"]);
        _device.IsEnabled = idle && !_working && _names.Count > 0;
        _refresh.IsEnabled = idle && !_working && !_listing;
        _countdown.IsEnabled = idle && !_working;
        var locked = idle && !_working ? null : Loc.Current["record.locked"];
        ToolTip.SetTip(_device, locked ?? (_names.Count == 0 ? Failure(CaptureFailure.NoDevice) : null));
        ToolTip.SetTip(_refresh, locked ?? Loc.Current["record.refreshTip"]);
        ToolTip.SetTip(_countdown, locked ?? Loc.Current["record.countdownTip"]);
        _gain.IsEnabled = phase != Phase.Stopped;
        ToolTip.SetTip(_gain, phase == Phase.Stopped ? Loc.Current["record.gainDone"] : Loc.Current["record.gainTip"]);
        _meter.IsVisible = phase != Phase.Stopped;
        _player.IsVisible = _info.IsVisible = _saveHint.IsVisible = phase == Phase.Stopped;
        _time.IsVisible = phase != Phase.Stopped; // the player shows the time of the take
        var ready = phase == Phase.Stopped && !_working && !_done;
        if (_create is not null)
        {
            _create.IsEnabled = ready;
            ToolTip.SetTip(_create, ready ? null : Loc.Current["record.takeFirst"]);
        }

        if (_put is not null)
        {
            _put.IsEnabled = ready && _canReplace;
            ToolTip.SetTip(_put, !ready ? Loc.Current["record.takeFirst"] : _canReplace ? Loc.Current["synth.replaceTip"] : Loc.Current["sounds.noSound"]);
        }

        ShowTime();
    }

    // A blocked privacy setting may have been switched on since the list was read, so it does not disable Record: opening reads it again and fails with the same message when it is still blocked.
    private bool Blocked => _problem is not null && !Denied;

    private void ShowTime() => _time.Text = Loc.Current.Format("record.time", ("time", Format(Take.Seconds)), ("size", (Take.Bytes / 1048576.0).ToString("0.0", CultureInfo.InvariantCulture)));

    private static string Format(double seconds) => TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss\.f" : @"m\:ss\.f", CultureInfo.InvariantCulture);

    private void ShowInfo()
    {
        var kept = _player.Region is var (start, end) ? Math.Min(end, _clip.Seconds) - start : _clip.Seconds;
        _info.Text = Loc.Current.Format("record.info", ("seconds", kept.ToString("0.00", CultureInfo.InvariantCulture)), ("total", _clip.Seconds.ToString("0.00", CultureInfo.InvariantCulture)));
    }

    // A new take starts empty: the old one, its region and the clipping mark are gone.
    private void Discard()
    {
        Transport.Stop();
        _clip = new PcmAudio([], 1, RecordingTake.SampleRate);
        _player.Clear();
        Take.Clear();
        _message.Text = "";
        _clipped.IsVisible = false;
    }

    private void Fail(CaptureFailure failure, Phase back = Phase.Idle)
    {
        ReleaseCapture();
        _message.Text = Failure(failure);
        SetPhase(back);
    }

    private static string Failure(CaptureFailure failure) => failure switch
    {
        CaptureFailure.LibraryMissing => Loc.Current.Format("record.failLibrary", ("file", AudioDevices.LibraryFile)),
        CaptureFailure.NoDevice => Loc.Current["record.failNoDevice"],
        CaptureFailure.Busy => Loc.Current["record.failBusy"],
        CaptureFailure.AccessDenied => Loc.Current["record.failDenied"],
        _ => Loc.Current["record.failLost"],
    };

    // The microphone goes back off the UI thread (closing a driver can take a moment).
    private void ReleaseCapture()
    {
        var capture = _capture;
        _capture = null;
        ReleaseLater(capture);
    }

    private static void ReleaseLater(IAudioCapture? capture)
    {
        if (capture is not null) _ = Task.Run(() => AudioCaptureDevices.Release(capture));
    }

    // The take as the WAV that is stored: the region when there is one, else all of it. Generating is deterministic, so these are the bytes the player shows.
    private byte[] Wav() => AudioCodec.Encode(_player.Cut(_clip), "wav");

    // The button stays enabled while the owner works, so a second click must not start a second edit.
    private async Task ReplaceAsync(Func<byte[], Task<bool>>? replace)
    {
        if (replace is null || _working || _done || _phase != Phase.Stopped) return;
        _working = true;
        Update();
        try
        {
            if (await replace(Wav()))
            {
                (_done, _phase) = (true, Phase.Idle); // the take is in the sound now: closing loses nothing
                Close(true);
            }
        }
        finally
        {
            _working = false;
            if (!_closed) Update();
        }
    }
}
