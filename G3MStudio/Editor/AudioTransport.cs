using System.Diagnostics;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using G3MStudio.Audio;
using G3MStudio.Core.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// Play, pause, stop, a seek bar with the time, loop, a region to play (the trim preview) and volume for one sound, over a waveform with a moving cursor: a click on the waveform seeks,
/// a drag selects the region (the owner keeps its start and end and gets <see cref="RegionChanged"/>), and play and loop stay inside the region. The audio is decoded once on a
/// pool thread when the control shows, which also draws the waveform. The output comes from <see cref="AudioDevices"/>; without a device <see cref="NoDevice"/> runs instead and nothing plays here.
/// </summary>
public sealed partial class AudioTransport : StackPanel
{
    private readonly Func<PcmAudio> _decode;
    private readonly Func<AudioOptions> _options;
    private readonly Func<(double Start, double End)?> _region;
    private readonly WaveformView _wave;
    private readonly Button _play, _stop, _playRegion, _clearRegion, _zoomIn, _zoomOut, _zoomFit;
    private readonly ScrollBar _scroll;
    private readonly CheckBox _vizShow;
    private long _lastTick;
    private int _zoomTips = -1;
    private readonly Slider _seek, _volume;
    private readonly CheckBox _loop;
    private readonly TextBlock _time;
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private Task<PcmAudio?>? _decoding;
    private double _duration;
    private IAudioOutput? _output, _hooked;
    private double _resume; // where a play from the bar starts while nothing plays
    private double _volumePercent;
    private bool _syncing, _scrubbing, _viewSyncing, _following, _free; // _free: the user scrolled the waveform, so the cursor is not followed until the next play
    private PcmAudio? _clip;

    /// <summary>Stores the visualizer toggle (the owner keeps it in the settings); null keeps it for this session only.</summary>
    public static Action<bool>? RememberVisualizer { get; set; }

    /// <summary>Stores the waveform height the user dragged to (pixels); null keeps it for this session only.</summary>
    public static Action<double>? RememberWaveformHeight { get; set; }

    internal AudioVisualizer Visualizer { get; } = new();

    /// <param name="duration">Seconds of the sound, for the bar before the audio is decoded; the decoded length replaces it.</param>
    /// <param name="decode">Decodes the audio; runs on a pool thread.</param>
    /// <param name="options">The playback options (device, volume, buffer) read at each play.</param>
    /// <param name="region">The part the region button plays, or null for none.</param>
    /// <param name="playName">Automation name of the play button.</param>
    public AudioTransport(double duration, Func<PcmAudio> decode, Func<AudioOptions> options, Func<(double Start, double End)?>? region = null, string playName = "AudioPlay")
    {
        (_duration, _decode, _options) = (Math.Max(0.01, duration), decode, options);
        _region = region ?? (() => null);
        _volumePercent = options().Volume;
        Name = "AudioTransport";
        Spacing = 4;
        _wave = new WaveformView();
        _wave.SeekRequested += fraction => SeekTo(fraction * _duration);
        _wave.RegionDragged += OnRegionDragged;
        _wave.ViewChanged += OnViewChanged;
        _scroll = new ScrollBar { Name = "WaveScroll", Orientation = Orientation.Horizontal, Minimum = 0, Maximum = 0, ViewportSize = 1, Visibility = ScrollBarVisibility.Auto }; // shows itself only while there is something to scroll
        _scroll.ValueChanged += (_, e) =>
        {
            if (!_viewSyncing) _wave.SetView(e.NewValue, _wave.ViewSpan);
        };
        _vizShow = new CheckBox { Name = "AudioVisualizerShow", Content = Loc.Current["audio.visualizer"], IsChecked = options().Visualizer };
        ToolTip.SetTip(_vizShow, Loc.Current["audio.visualizerTip"]);
        Visualizer.IsVisible = _vizShow.IsChecked == true;
        _vizShow.IsCheckedChanged += (_, _) =>
        {
            Visualizer.IsVisible = _vizShow.IsChecked == true;
            RememberVisualizer?.Invoke(Visualizer.IsVisible);
            if (Visualizer.IsVisible && !Visualizer.Settled) StartTicker(); // bars left lit while it was hidden fall to zero
        };
        _zoomIn = ZoomButton("ZoomIn", "+", "audio.zoomIn", "audio.zoomInTip", () => _wave.Zoom(2, ZoomAnchor()));
        _zoomOut = ZoomButton("ZoomOut", "-", "audio.zoomOut", "audio.zoomOutTip", () => _wave.Zoom(0.5, ZoomAnchor()));
        _zoomFit = ZoomButton("ZoomFit", Loc.Current["audio.fit"], "audio.fit", "audio.fitTip", () => _wave.SetView(0, 1));
        var hint = Loc.Current["audio.regionHint"];
        ToolTip.SetTip(_wave, region is null ? null : hint);
        _play = new Button { Name = playName, Content = Loc.Current["sounds.play"] };
        _stop = new Button { Name = "AudioStop", Content = Loc.Current["audio.stop"] };
        _playRegion = new Button { Name = "AudioPlayRegion", Content = Loc.Current["audio.playRegion"], IsVisible = region is not null };
        _clearRegion = new Button { Name = "AudioClearRegion", Content = Loc.Current["audio.clearRegion"], IsVisible = region is not null };
        ToolTip.SetTip(_clearRegion, Loc.Current["audio.clearRegionTip"]);
        ToolTip.SetTip(_playRegion, Loc.Current["audio.playRegionTip"]);
        _seek = new Slider { Name = "AudioSeek", Minimum = 0, Maximum = _duration, LargeChange = 1, SmallChange = 0.1, MinWidth = 160, VerticalAlignment = VerticalAlignment.Center };
        _time = new TextBlock { Name = "AudioTime", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
        _loop = new CheckBox { Name = "AudioLoop", Content = Loc.Current["audio.loop"] };
        ToolTip.SetTip(_loop, Loc.Current[region is null ? "audio.loopTip" : "audio.loopRegionTip"]);
        _volume = new Slider { Name = "AudioVolume", Minimum = 0, Maximum = 100, Value = _volumePercent, MinWidth = 100, VerticalAlignment = VerticalAlignment.Center };
        _play.Click += (_, _) => _ = PlayPauseAsync();
        _stop.Click += (_, _) => Stop();
        _playRegion.Click += (_, _) => _ = PlayRegionAsync();
        _clearRegion.Click += (_, _) => ClearRegion();
        _seek.PropertyChanged += (_, e) =>
        {
            if (e.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty || _syncing) return;
            if (_scrubbing) Refresh(); // the thumb is being dragged: show where it is and seek when it is let go
            else SeekTo(_seek.Value);
        };
        _seek.AddHandler(PointerPressedEvent, (_, e) => _scrubbing = e.GetCurrentPoint(_seek).Properties.IsLeftButtonPressed, Avalonia.Interactivity.RoutingStrategies.Tunnel, true);
        _seek.AddHandler(PointerReleasedEvent, (_, _) => EndScrub(), Avalonia.Interactivity.RoutingStrategies.Bubble, true);
        // Alt+Tab or a popup mid drag takes the pointer without a release; the capture is on the thumb, so the event is not seen higher up.
        _seek.TemplateApplied += (_, e) => e.NameScope.Find<Avalonia.Controls.Primitives.Thumb>("thumb")?.PointerCaptureLost += (_, _) => EndScrub();
        _loop.IsCheckedChanged += (_, _) =>
        {
            _output?.Loop = _loop.IsChecked == true;
        };
        _volume.PropertyChanged += (_, e) =>
        {
            if (e.Property != Avalonia.Controls.Primitives.RangeBase.ValueProperty) return;
            _volumePercent = _volume.Value;
            _output?.Volume = _volumePercent / 100;
        };
        var buttons = new WrapPanel { ItemSpacing = 8, LineSpacing = 6, Children = { _play, _stop, _playRegion, _clearRegion, _loop, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4, Children = { _zoomOut, _zoomIn, _zoomFit } }, _vizShow, new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = Loc.Current["audio.volume"], VerticalAlignment = VerticalAlignment.Center }, _volume } } } };
        Children.Add(_wave);
        Children.Add(DragHandle.For(_wave, "preview.audio", height => RememberWaveformHeight?.Invoke(height)));
        if (options().WaveformHeight > 0) _wave.Height = options().WaveformHeight; // the height dragged last, kept in the settings, over the one of this session
        Children.Add(_scroll);
        Children.Add(Visualizer);
        Children.Add(_seek);
        Children.Add(_time);
        Children.Add(buttons);
        Children.Add(new TextBlock { Name = "AudioRegionHint", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Hint, Text = hint, TextWrapping = Avalonia.Media.TextWrapping.Wrap, IsVisible = region is not null });
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _ticker.Tick += (_, _) => Tick();
        DetachedFromVisualTree += (_, _) => _ticker.Stop();
        AttachedToVisualTree += (_, _) =>
        {
            if (IsPlaying || !Visualizer.Settled) _ticker.Start();
            if (!_closed) _ = Decoded();
        };
        Refresh();
        RefreshZoom();
    }

    private static Button ZoomButton(string name, string text, string label, string tip, Action click)
    {
        var button = new Button { Name = name, Content = text, MinWidth = 32 };
        Avalonia.Automation.AutomationProperties.SetName(button, Loc.Current[label]);
        ToolTip.SetTip(button, Loc.Current[tip]);
        ToolTip.SetShowOnDisabled(button, true);
        button.Click += (_, _) => click();
        return button;
    }

    // Zooming keeps the cursor where it is on the screen while it is in view, else the middle of the view.
    private double ZoomAnchor() => _wave.Playhead is var head && !double.IsNaN(head) && head >= _wave.ViewStart && head <= _wave.ViewStart + _wave.ViewSpan ? head : _wave.ViewStart + _wave.ViewSpan / 2;

    private void OnViewChanged()
    {
        if (!_following) _free = true;
        _viewSyncing = true;
        (_scroll.Maximum, _scroll.ViewportSize, _scroll.Value, _scroll.LargeChange, _scroll.SmallChange) = (1 - _wave.ViewSpan, _wave.ViewSpan, _wave.ViewStart, _wave.ViewSpan, _wave.ViewSpan / 10);
        _viewSyncing = false;
        RefreshZoom();
    }

    // A zoom button that cannot act says why in its tip; the tips change only when the state does.
    private void RefreshZoom()
    {
        var whole = _wave.ViewSpan >= 1;
        var atMax = _wave.ViewSpan <= _wave.MinSpan;
        _zoomIn.IsEnabled = !atMax && _clip is not null;
        _zoomOut.IsEnabled = _zoomFit.IsEnabled = !whole;
        var state = (_clip is null ? 1 : 0) | (atMax ? 2 : 0) | (whole ? 4 : 0);
        if (state == _zoomTips) return;
        _zoomTips = state;
        ToolTip.SetTip(_zoomIn, Loc.Current[_clip is null ? "audio.zoomWait" : atMax ? "audio.zoomAtMax" : "audio.zoomInTip"]);
        ToolTip.SetTip(_zoomOut, Loc.Current[whole ? "audio.zoomAtAll" : "audio.zoomOutTip"]);
        ToolTip.SetTip(_zoomFit, Loc.Current[whole ? "audio.zoomAtAll" : "audio.fitTip"]);
    }

    /// <summary>Runs when no output device can be opened; the owner plays the sound another way.</summary>
    public Action? NoDevice { get; set; }

    /// <summary>Off while nothing plays and the visualizer is hidden or has settled, so an idle player costs nothing.</summary>
    internal bool Ticking => _ticker.IsEnabled;

    /// <summary>The sound plays, not paused.</summary>
    public bool IsPlaying => _output?.IsPlaying == true;

    /// <summary>The playing position in seconds (the bar position while nothing plays).</summary>
    public double Position => _output?.IsActive == true ? _output.Position : _resume;

    public string TimeText => _time.Text ?? "";

    /// <summary>Runs before a sound starts, so the owner can finish a change that is still waiting.</summary>
    internal Action? BeforePlay { get; init; }

    /// <summary>Puts other audio in the player (the generator made a new sound): playing stops, the waveform and the length follow, and the whole sound is shown.</summary>
    public void Replace(PcmAudio clip)
    {
        Stop();
        (_clip, _decoding, _duration) = (clip, Task.FromResult<PcmAudio?>(clip), Math.Max(0.01, clip.Seconds));
        _seek.Maximum = _duration;
        _wave.Source = new WavePeaks(clip);
        _wave.SetView(0, 1);
        Refresh();
        RefreshZoom();
    }

    /// <summary>Plays the sound from the bar (inside the region when there is one), or pauses, or continues.</summary>
    public async Task PlayPauseAsync()
    {
        if (_output is { IsPlaying: true }) _output.Pause();
        else if (_output is { IsActive: true })
        {
            _output.Resume();
            StartTicker();
        }
        else
        {
            BeforePlay?.Invoke();
            await Decoded(); // the region is clamped to the decoded length
            if (ActiveRegion() is var (start, end)) await StartAsync(_resume >= start && _resume < end ? _resume : start, end);
            else await StartAsync(_resume, 0);
        }

        Refresh();
    }

    /// <summary>Plays the region set by the owner (looped when Loop is on); nothing when it is empty.</summary>
    public async Task PlayRegionAsync()
    {
        BeforePlay?.Invoke();
        await Decoded();
        if (ActiveRegion() is not var (start, end)) return;
        await StartAsync(start, end);
        Refresh();
    }

    /// <summary>The seek thumb was let go: continue from where it is. A drag moves only the cursor and the time, so the sound does not restart at every step.</summary>
    public void EndScrub()
    {
        if (!_scrubbing) return;
        _scrubbing = false;
        SeekTo(_seek.Value);
    }

    public void ToggleLoop() => _loop.IsChecked = _loop.IsChecked != true;

    public void Stop()
    {
        _output?.Stop();
        _resume = 0;
        Refresh();
    }

    /// <summary>Stops the sound and gives the device back (the tab or window closes).</summary>
    public void Close()
    {
        _closed = true;
        _scrubbing = false;
        _ticker.Stop();
        AudioDevices.Release(this);
        Unhook();
        _output = null;
        _resume = 0;
        Refresh();
    }

    /// <summary>Seeks while playing or paused; else sets where the next play starts.</summary>
    public void SeekTo(double seconds)
    {
        seconds = Math.Clamp(seconds, 0, _duration);
        if (_output?.IsActive == true) _output.Seek(seconds);
        else _resume = seconds;
        Refresh();
    }

    /// <summary>Moves the cursor and the time to the output position; the timer calls this while a sound plays.</summary>
    public void Tick()
    {
        if (_output is { IsActive: false }) _resume = 0; // the sound reached its end
        Refresh();
        var now = Stopwatch.GetTimestamp();
        var seconds = Stopwatch.GetElapsedTime(_lastTick, now).TotalSeconds;
        _lastTick = now;
        if (Visualizer.IsVisible) Visualizer.Step(_clip, Position, IsPlaying, seconds); // after a stop or a pause the bars fall to zero, so the timer runs until they have
        if (!IsPlaying && (Visualizer.Settled || !Visualizer.IsVisible)) _ticker.Stop(); // a paused sound moves nothing once the bars have settled
    }

    private bool _closed;

    private void StartTicker()
    {
        _lastTick = Stopwatch.GetTimestamp();
        _ticker.Start();
    }

    private async Task<IAudioOutput?> StartAsync(double start, double end)
    {
        var clip = await Decoded();
        if (clip is null) NoDevice?.Invoke(); // a sound this decoder cannot read is played by the system player
        if (clip is null || _closed) return null; // closed while decoding: do not take the device back
        var options = _options();
        if (AudioDevices.IsSilent(options)) return null; // the user turned sound off
        var output = AudioDevices.Acquire(options, this, () => { Unhook(); _output = null; Dispatcher.UIThread.Post(Tick); }); // another player took the device
        if (output is null)
        {
            NoDevice?.Invoke();
            return null;
        }

        _output = output;
        if (!ReferenceEquals(_hooked, output))
        {
            Unhook();
            _hooked = output;
            output.Ended += OnEnded;
        }

        output.Load(clip);
        (output.Volume, output.Loop) = (_volumePercent / 100, _loop.IsChecked == true);
        output.Play(start, end);
        _free = false;
        StartTicker();
        return output;
    }

    private void OnEnded() => Dispatcher.UIThread.Post(Tick);

    // The output outlives the tab, so the tab must not stay subscribed to it.
    private void Unhook()
    {
        _hooked?.Ended -= OnEnded;
        _hooked = null;
    }

    // One decode serves the waveform and every play, even when Play is pressed twice while it runs.
    private Task<PcmAudio?> Decoded() => _decoding ??= DecodeAsync();

    private async Task<PcmAudio?> DecodeAsync()
    {
        try
        {
            var (clip, peaks) = await Task.Run(() =>
            {
                var decoded = _decode();
                return (decoded, new WavePeaks(decoded));
            });
            (_clip, _wave.Source) = (clip, peaks);
            RefreshZoom();
            if (clip.Seconds > 0 && Math.Abs(clip.Seconds - _duration) > 0.001) // the header length can be missing (MP3) or a little off
            {
                _duration = clip.Seconds;
                _seek.Maximum = _duration;
                Refresh();
            }

            return clip;
        }
        catch (Exception e) when (e is InvalidOperationException or IOException or InvalidDataException or NotSupportedException or ArgumentException or OutOfMemoryException) // what a decoder or the memory refuses: the sound goes to the system player, and the cached task does not stay faulted
        {
            return null;
        }
    }

    private void Refresh()
    {
        var position = _scrubbing ? _seek.Value : Position;
        var active = _output?.IsActive == true;
        if (!_scrubbing)
        {
            _syncing = true;
            _seek.Value = position;
            _syncing = false;
        }

        _wave.Playhead = active || _resume > 0 || _scrubbing ? position / _duration : double.NaN;
        if (active && !_free && !double.IsNaN(_wave.Playhead))
        {
            _following = true; // the view follows the cursor; this move is not the user's
            _wave.Reveal(_wave.Playhead);
            _following = false;
        }

        var region = ActiveRegion();
        _wave.Region = region is var (start, end) ? (start / _duration, end / _duration) : null;
        _play.Content = Loc.Current[IsPlaying ? "audio.pause" : "sounds.play"];
        _time.Text = $"{Format(position)} / {Format(_duration)}" + (region is var (from, to) ? $"   [{Format(from)} - {Format(to)}]" : "");
    }

    private static string Format(double seconds) => TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss\.f" : @"m\:ss\.f", CultureInfo.InvariantCulture);

    // GameMaker's sound editor keys: Space plays or pauses, L toggles the loop, R rewinds.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox or NumericUpDown) return;
        var plain = e.KeyModifiers == KeyModifiers.None;
        if (!plain && e.KeyModifiers != KeyModifiers.Shift) return; // Shift is allowed for the zoom keys: "+" is Shift+= on a US layout
        switch (e.Key)
        {
            case Key.Space when plain: _ = PlayPauseAsync(); break;
            case Key.L when plain: _loop.IsChecked = _loop.IsChecked != true; break;
            case Key.R when plain: SeekTo(0); break;
            case Key.Left or Key.Right when e.Source == _wave && NudgeEdge(e.Key == Key.Right ? 1 : -1, !plain): break;
            case Key.OemPlus or Key.Add: _wave.Zoom(2, ZoomAnchor()); break;
            case Key.OemMinus or Key.Subtract: _wave.Zoom(0.5, ZoomAnchor()); break;
            default: return;
        }

        e.Handled = true;
    }
}
