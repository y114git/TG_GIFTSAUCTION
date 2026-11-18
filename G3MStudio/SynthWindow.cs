using Avalonia.Controls;
using Avalonia.Threading;
using G3MStudio.Core.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>
/// The sound generator: pick a preset or a random sound, change the values, hear and see the result at once (the player and waveform of the audio editor), then make a new sound of it or put it into the
/// sound selected. The owner does the edit; a callback returns the reason it was refused, which stays in the dialog next to the buttons so the name or the choice can be fixed.
/// </summary>
public sealed class SynthWindow : Window
{
    private static readonly (SynthPreset Preset, string Key)[] Presets =
    [
        (SynthPreset.Jump, "synth.presetJump"), (SynthPreset.Coin, "synth.presetCoin"), (SynthPreset.Hit, "synth.presetHit"), (SynthPreset.Laser, "synth.presetLaser"),
        (SynthPreset.Explosion, "synth.presetExplosion"), (SynthPreset.Blip, "synth.presetBlip"), (SynthPreset.PowerUp, "synth.presetPowerUp"),
    ];

    private static readonly (SynthShape Shape, string Key)[] Shapes =
    [
        (SynthShape.Sine, "synth.shapeSine"), (SynthShape.Square, "synth.shapeSquare"), (SynthShape.Saw, "synth.shapeSaw"), (SynthShape.Triangle, "synth.shapeTriangle"), (SynthShape.Noise, "synth.shapeNoise"),
    ];

    private readonly ComboBox _shape = new() { Name = "SynthShape", Width = 180 };
    private readonly NumericUpDown _duty = Field("SynthDuty", 50, 5, 95, 5, "0");
    private readonly NumericUpDown _frequency = Field("SynthFrequency", 440, SoundSynth.MinFrequency, SoundSynth.MaxFrequency, 10, "0");
    private readonly NumericUpDown _slide = Field("SynthSlide", 0, -SoundSynth.MaxSlide, SoundSynth.MaxSlide, 0.1m);
    private readonly NumericUpDown _vibratoDepth = Field("SynthVibratoDepth", 0, 0, SoundSynth.MaxVibrato, 0.1m);
    private readonly NumericUpDown _vibratoSpeed = Field("SynthVibratoSpeed", 6, 0, SoundSynth.MaxVibratoSpeed, 0.5m);
    private readonly NumericUpDown _attack = Field("SynthAttack", 0.01m, 0, SoundSynth.MaxEnvelope, 0.01m, "0.000");
    private readonly NumericUpDown _sustain = Field("SynthSustain", 0.1m, 0, SoundSynth.MaxEnvelope, 0.01m, "0.000");
    private readonly NumericUpDown _decay = Field("SynthDecay", 0.2m, 0, SoundSynth.MaxEnvelope, 0.01m, "0.000");
    private readonly NumericUpDown _duration = Field("SynthDuration", 0.4m, SoundSynth.MinDuration, SoundSynth.MaxDuration, 0.05m);
    private readonly NumericUpDown _volume = Field("SynthVolume", 80, 0, 100, 5, "0");
    private readonly NumericUpDown _lowPass = Field("SynthLowPass", 0, 0, SoundSynth.MaxFrequency, 100, "0");
    private readonly NumericUpDown _seed = Field("SynthSeed", 1, 0, int.MaxValue, 1, "0");
    private readonly TextBox _name = new() { Name = "SynthName" };
    private readonly TextBlock _info = new() { Name = "SynthInfo", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextBlock _message = new() { Name = "SynthMessage", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly DispatcherTimer _pending = new() { Interval = TimeSpan.FromMilliseconds(60) };
    private PcmAudio _clip = new([], 1, SoundSynth.SampleRate);
    private bool _building, _replacing;

    /// <param name="create">Makes a new sound of the name and the WAV and returns why it was refused, or null; null hides the name field and the button.</param>
    /// <param name="replace">Puts the WAV into the sound and returns whether that was done; null turns the button off.</param>
    /// <param name="replaceName">The name of the sound the audio would replace, as it is shown to the user.</param>
    /// <param name="audio">The playback options of the player.</param>
    /// <param name="suggestName">A free name for a new sound.</param>
    public SynthWindow(Func<string, byte[], string?>? create, Func<byte[], Task<bool>>? replace, string? replaceName, Func<AudioOptions> audio, Func<string>? suggestName = null)
    {
        Title = Loc.Current["synth.title"];
        Width = 560;
        MinWidth = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        foreach (var (_, key) in Shapes) _shape.Items.Add(Loc.Current[key]);
        _shape.SelectedIndex = 1;
        _name.Text = suggestName?.Invoke() ?? "snd_generated";
        _seed.Value = 1;
        var current = SoundSynth.Generate(new SynthSettings());
        Player = new RegionPlayer(current.Seconds, () => current, audio, "SynthPlay", "SynthRegion", Flush);
        // A run of changes (a held spin arrow) makes the sound once, a moment after the last one; playing, creating and replacing make it first.
        _pending.Tick += (_, _) => Regenerate();
        foreach (var box in new[] { _duty, _frequency, _slide, _vibratoDepth, _vibratoSpeed, _attack, _sustain, _decay, _duration, _volume, _lowPass, _seed }) box.ValueChanged += (_, _) => Changed();
        _shape.SelectionChanged += (_, _) => Changed();
        ToolTip.SetTip(_duty, Loc.Current["synth.dutyTip"]);
        ToolTip.SetShowOnDisabled(_duty, true);
        ToolTip.SetTip(_seed, Loc.Current["synth.seedTip"]);

        var presets = new WrapPanel { ItemSpacing = 6, LineSpacing = 6 };
        presets.Children.Add(new TextBlock { Text = Loc.Current["synth.presets"], VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
        foreach (var (preset, key) in Presets)
        {
            var button = new Button { Name = "SynthPreset" + preset, Content = Loc.Current[key] };
            button.Click += (_, _) => Apply(SoundSynth.Preset(preset, Seed()));
            presets.Children.Add(button);
        }

        var random = new Button { Name = "SynthRandom", Content = Loc.Current["synth.random"] };
        ToolTip.SetTip(random, Loc.Current["synth.randomTip"]);
        random.Click += (_, _) => Apply(SoundSynth.Random(System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, 1_000_000)));
        presets.Children.Add(random);

        var rows = new StackPanel { Spacing = 8 };
        rows.Children.Add(presets);
        rows.Children.Add(Player);
        rows.Children.Add(_info);
        rows.Children.Add(DialogFrame.Form(
            (Loc.Current["synth.shape"], _shape), (Loc.Current["synth.duty"], _duty), (Loc.Current["synth.frequency"], _frequency), (Loc.Current["synth.slide"], _slide),
            (Loc.Current["synth.vibratoDepth"], _vibratoDepth), (Loc.Current["synth.vibratoSpeed"], _vibratoSpeed), (Loc.Current["synth.attack"], _attack), (Loc.Current["synth.sustain"], _sustain),
            (Loc.Current["synth.decay"], _decay), (Loc.Current["synth.duration"], _duration), (Loc.Current["synth.volume"], _volume), (Loc.Current["synth.lowPass"], _lowPass), (Loc.Current["synth.seed"], _seed)));
        if (create is not null) rows.Children.Add(DialogFrame.Form((Loc.Current["synth.name"], _name)));

        var actions = new List<Control>();
        if (create is not null)
            actions.Add(DialogFrame.Action(this, Loc.Current["synth.create"], "SynthCreate", () =>
            {
                var name = (_name.Text ?? "").Trim();
                if (_replacing) return;
                if (name.Length == 0) _message.Text = Loc.Current["synth.noName"];
                else if (create(name, Wav()) is { } refused) _message.Text = refused;
                else Close(true);
            }, DialogRole.Primary));
        var put = DialogFrame.Action(this, replaceName is null ? Loc.Current["synth.replaceNone"] : Loc.Current.Format("synth.replace", ("sound", replaceName)), "SynthReplace", () => _ = ReplaceAsync(replace), create is null ? DialogRole.Primary : DialogRole.Normal);
        put.IsEnabled = replace is not null && replaceName is not null;
        ToolTip.SetTip(put, put.IsEnabled ? Loc.Current["synth.replaceTip"] : Loc.Current["sounds.noSound"]);
        ToolTip.SetShowOnDisabled(put, true);
        actions.Add(put);
        actions.Add(DialogFrame.Cancel(this, () => Close(false), "SynthCancel"));
        Closed += (_, _) =>
        {
            _pending.Stop();
            Transport.Close();
        };
        Regenerate();
        DialogFrame.Show(this, rows, actions, new StackPanel { Spacing = 4, Children = { new TextBlock { Name = "SynthSaveHint", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Hint, Text = Loc.Current["audio.saveRegionHint"], TextWrapping = Avalonia.Media.TextWrapping.Wrap }, _message } }, DialogScroll.Vertical);
    }

    /// <summary>Plays the clip of the current values; every change swaps in a new clip and stops what plays.</summary>
    public AudioTransport Transport => Player.Transport;

    /// <summary>The player with the fields of its region: the region is the part that Create and Replace keep.</summary>
    internal RegionPlayer Player { get; }

    /// <summary>The boxes as a settings record (the generator limits any value to its range).</summary>
    public SynthSettings Current() => new()
    {
        Shape = Shapes[Math.Max(0, _shape.SelectedIndex)].Shape,
        Duty = Number(_duty) / 100,
        Frequency = Number(_frequency),
        Slide = Number(_slide),
        VibratoDepth = Number(_vibratoDepth),
        VibratoSpeed = Number(_vibratoSpeed),
        Attack = Number(_attack),
        Sustain = Number(_sustain),
        Decay = Number(_decay),
        Duration = Number(_duration),
        Volume = Number(_volume) / 100,
        LowPass = Number(_lowPass),
        Seed = Seed(),
    };

    /// <summary>Sets every value from the settings (a preset or a random sound) and draws the result once.</summary>
    public void Apply(SynthSettings settings)
    {
        var s = SoundSynth.Limited(settings);
        _building = true;
        _shape.SelectedIndex = Array.FindIndex(Shapes, x => x.Shape == s.Shape);
        (_duty.Value, _frequency.Value, _slide.Value, _vibratoDepth.Value, _vibratoSpeed.Value) = ((decimal)Math.Round(s.Duty * 100), (decimal)Math.Round(s.Frequency), Round(s.Slide), Round(s.VibratoDepth), Round(s.VibratoSpeed));
        (_attack.Value, _sustain.Value, _decay.Value, _duration.Value) = (Round(s.Attack), Round(s.Sustain), Round(s.Decay), Round(s.Duration));
        (_volume.Value, _lowPass.Value, _seed.Value) = ((decimal)Math.Round(s.Volume * 100), (decimal)Math.Round(s.LowPass), s.Seed);
        _building = false;
        Player.Clear(); // a new sound, so the region of the old one means nothing
        Regenerate();
    }

    private static decimal Round(double value) => (decimal)Math.Round(value, 3);

    private static double Number(NumericUpDown box) => (double)(box.Value ?? 0);

    private int Seed() => (int)Math.Clamp(_seed.Value ?? 0, 0, int.MaxValue);

    private static NumericUpDown Field(string name, decimal value, double min, double max, decimal step, string format = "0.0#") =>
        new() { Name = name, Value = value, Minimum = (decimal)min, Maximum = (decimal)max, Increment = step, FormatString = format, Width = 140 };

    /// <summary>How many times the sound was made from the boxes.</summary>
    internal int Renders { get; private set; }

    private void Changed()
    {
        if (_building) return;
        _pending.Stop();
        _pending.Start();
    }

    private void Flush()
    {
        if (_pending.IsEnabled) Regenerate();
    }

    private byte[] Wav()
    {
        Flush();
        return AudioCodec.Encode(Player.Cut(_clip), "wav"); // the clip on show, cut to the region when there is one: generating is deterministic, so these are the bytes of the boxes
    }

    // The duty only shapes a square wave: the box is off for the others, and its tip says why.
    private void Regenerate()
    {
        _pending.Stop();
        if (_building) return;
        var settings = Current();
        _duty.IsEnabled = settings.Shape == SynthShape.Square;
        _clip = SoundSynth.Generate(settings);
        Renders++;
        Transport.Replace(_clip);
        _info.Text = Loc.Current.Format("synth.info", ("seconds", _clip.Seconds.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
        _message.Text = "";
    }

    // The button stays enabled while the owner works, so a second click must not start a second edit.
    private async Task ReplaceAsync(Func<byte[], Task<bool>>? replace)
    {
        if (replace is null || _replacing) return;
        _replacing = true;
        try
        {
            if (await replace(Wav())) Close(true);
        }
        finally
        {
            _replacing = false;
        }
    }
}
