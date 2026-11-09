using System.Globalization;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using G3MStudio.Audio;
using G3MStudio.Core.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// Play, pause, stop, a seek bar with the time, loop, a region to play (the trim preview) and volume for one sound, over a waveform with a moving cursor. The audio is decoded on the first
/// play. The output comes from <see cref="AudioDevices"/>; without a device <see cref="NoDevice"/> runs instead and nothing plays here.
/// </summary>
public sealed class AudioTransport : StackPanel
{
    private readonly double _duration;
    private readonly Func<PcmAudio> _decode;
    private readonly Func<AudioOptions> _options;
    private readonly Func<(double Start, double End)?> _region;
    private readonly WaveformView _wave;
    private readonly Button _play, _stop, _playRegion;
    private readonly Slider _seek, _volume;
    private readonly CheckBox _loop;
    private readonly TextBlock _time;
    private readonly DispatcherTimer _ticker = new() { Interval = TimeSpan.FromMilliseconds(33) };
    private PcmAudio? _clip;
    private IAudioOutput? _output, _hooked;
    private double _resume; // where a play from the bar starts while nothing plays
    private double _volumePercent;
    private bool _syncing;

    /// <param name="duration">Seconds of the sound, for the bar before the audio is decoded.</param>
    /// <param name="decode">Decodes the audio; runs on a pool thread at the first play.</param>
    /// <param name="peaks">The waveform if it is known without decoding, else null (it is drawn after the first play).</param>
    /// <param name="options">The playback options (device, volume, buffer) read at each play.</param>
    /// <param name="region">The part the region button plays, or null for none.</param>
    /// <param name="playName">Automation name of the play button.</param>
    public AudioTransport(double duration, Func<PcmAudio> decode, float[]? peaks, Func<AudioOptions> options, Func<(double Start, double End)?>? region = null, string playName = "AudioPlay")
    {
        (_duration, _decode, _options) = (Math.Max(0.01, duration), decode, options);
        _region = region ?? (() => null);
        _volumePercent = options().Volume;
        Name = "AudioTransport";
        Spacing = 4;
        _wave = new WaveformView(peaks ?? new float[WaveformView.Buckets]);
        _wave.SeekRequested += fraction => SeekTo(fraction * _duration);
        _play = new Button { Name = playName, Content = Loc.Current["sounds.play"] };
        _stop = new Button { Name = "AudioStop", Content = Loc.Current["audio.stop"] };
        _playRegion = new Button { Name = "AudioPlayRegion", Content = Loc.Current["audio.playRegion"], IsVisible = region is not null };
        _seek = new Slider { Name = "AudioSeek", Minimum = 0, Maximum = _duration, LargeChange = 1, SmallChange = 0.1, MinWidth = 160, VerticalAlignment = VerticalAlignment.Center };
        _time = new TextBlock { Name = "AudioTime", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
        _loop = new CheckBox { Name = "AudioLoop", Content = Loc.Current["audio.loop"] };
        _volume = new Slider { Name = "AudioVolume", Minimum = 0, Maximum = 100, Value = _volumePercent, MinWidth = 100, VerticalAlignment = VerticalAlignment.Center };
        _play.Click += (_, _) => _ = PlayPauseAsync();
        _stop.Click += (_, _) => Stop();
        _playRegion.Click += (_, _) => _ = PlayRegionAsync();
        _seek.PropertyChanged += (_, e) =>
        {
            if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty && !_syncing) SeekTo(_seek.Value);
        };
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
        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { _play, _stop, _playRegion, _loop, new TextBlock { Text = Loc.Current["audio.volume"], VerticalAlignment = VerticalAlignment.Center }, _volume } };
        Children.Add(_wave);
        Children.Add(_seek);
        Children.Add(_time);
        Children.Add(buttons);
        AddHandler(KeyDownEvent, OnKeyDown, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _ticker.Tick += (_, _) => Tick();
        DetachedFromVisualTree += (_, _) => _ticker.Stop();
        AttachedToVisualTree += (_, _) =>
        {
            if (_output?.IsActive == true) _ticker.Start();
        };
        Refresh();
    }

    /// <summary>Runs when no output device can be opened; the owner plays the sound another way.</summary>
    public Action? NoDevice { get; set; }

    /// <summary>The sound plays, not paused.</summary>
    public bool IsPlaying => _output?.IsPlaying == true;

    /// <summary>The playing position in seconds (the bar position while nothing plays).</summary>
    public double Position => _output?.IsActive == true ? _output.Position : _resume;

    public string TimeText => _time.Text ?? "";

    /// <summary>The region changed: draw it again.</summary>
    public void Redraw() => Refresh();

    /// <summary>Plays the whole sound from the bar, or pauses, or continues.</summary>
    public async Task PlayPauseAsync()
    {
        if (_output is { IsPlaying: true }) _output.Pause();
        else if (_output is { IsActive: true }) _output.Resume();
        else await StartAsync(_resume, 0);

        Refresh();
    }

    /// <summary>Plays the region set by the owner (looped when Loop is on); nothing when it is empty.</summary>
    public async Task PlayRegionAsync()
    {
        if (_region() is not var (start, end) || end <= start) return;
        await StartAsync(start, end);
        Refresh();
    }

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
        if (_output?.IsActive != true) _ticker.Stop();
    }

    private bool _closed;

    private async Task<IAudioOutput?> StartAsync(double start, double end)
    {
        _clip ??= await DecodeAsync();
        if (_clip is null || _closed) return null; // closed while decoding: do not take the device back
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

        output.Load(_clip);
        (output.Volume, output.Loop) = (_volumePercent / 100, _loop.IsChecked == true);
        output.Play(start, end);
        _ticker.Start();
        return output;
    }

    private void OnEnded() => Dispatcher.UIThread.Post(Tick);

    // The output outlives the tab, so the tab must not stay subscribed to it.
    private void Unhook()
    {
        _hooked?.Ended -= OnEnded;
        _hooked = null;
    }

    private async Task<PcmAudio?> DecodeAsync()
    {
        try
        {
            var clip = await Task.Run(_decode);
            _wave.SetPeaks(WaveformView.Peaks(clip));
            return clip;
        }
        catch (InvalidOperationException)
        {
            NoDevice?.Invoke(); // a sound this decoder cannot read is played by the system player
            return null;
        }
    }

    private void Refresh()
    {
        var position = Position;
        var active = _output?.IsActive == true;
        _syncing = true;
        _seek.Value = position;
        _syncing = false;
        _wave.Playhead = active || _resume > 0 ? position / _duration : double.NaN;
        _wave.Region = _region() is var (start, end) && end > start ? (start / _duration, Math.Min(1, end / _duration)) : null;
        _play.Content = Loc.Current[IsPlaying ? "audio.pause" : "sounds.play"];
        _time.Text = $"{Format(position)} / {Format(_duration)}";
    }

    private static string Format(double seconds) => TimeSpan.FromSeconds(seconds).ToString(seconds >= 3600 ? @"h\:mm\:ss\.f" : @"m\:ss\.f", CultureInfo.InvariantCulture);

    // GameMaker's sound editor keys: Space plays or pauses, L toggles the loop, R rewinds.
    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is TextBox or NumericUpDown || e.KeyModifiers != KeyModifiers.None) return;
        switch (e.Key)
        {
            case Key.Space: _ = PlayPauseAsync(); break;
            case Key.L: _loop.IsChecked = _loop.IsChecked != true; break;
            case Key.R: SeekTo(0); break;
            default: return;
        }

        e.Handled = true;
    }
}
