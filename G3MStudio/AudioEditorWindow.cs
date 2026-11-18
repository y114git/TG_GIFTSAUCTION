using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>
/// The audio editor of a WAV or OGG sound: shows its waveform and cuts it to a start and end, scales its volume and fades its ends. Apply hands the new WAV
/// back; the caller makes the one undoable edit of the sound. <c>generate</c> and <c>record</c> add buttons that open the sound generator and the recorder on this sound.
/// </summary>
public sealed class AudioEditorWindow : Window
{
    private readonly PcmAudio _audio;
    private readonly string _kind;
    private readonly NumericUpDown _start = Field("AudioStart", 0, 0, 100000, 0.05m);
    private readonly NumericUpDown _end = Field("AudioEnd", 0, 0, 100000, 0.05m);
    private readonly NumericUpDown _gain = Field("AudioGain", 100, 0, 1600, 5, "0");
    private readonly NumericUpDown _fadeIn = Field("AudioFadeIn", 0, 0, 100000, 0.05m);
    private readonly NumericUpDown _fadeOut = Field("AudioFadeOut", 0, 0, 100000, 0.05m);
    /// <summary>Plays the audio, or the range between Start and End as a preview of the cut.</summary>
    public AudioTransport Transport { get; }

    private readonly WrapPanel _makers = new() { ItemSpacing = 8, LineSpacing = 6 };
    private readonly Button _mic = new() { Name = "AudioRecord", Content = Loc.Current["cmd.edit.recordSound"], IsVisible = false };
    private readonly TextBlock _info = new() { Name = "AudioInfo", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail };
    private readonly TextBlock _message = new() { Name = "AudioMessage", TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public AudioEditorWindow(string title, byte[] wav, Action<byte[]> apply, Func<G3MStudio.Core.Settings.AudioOptions>? audio = null, PcmAudio? decoded = null, Action<AudioEditorWindow>? generate = null)
        : this(title, wav, bytes =>
        {
            apply(bytes);
            return Task.FromResult(true);
        }, audio, decoded, generate)
    {
    }

    /// <summary><paramref name="apply"/> returns false when the sound was not changed (a declined warning, a refused edit): the window then stays open with a message.</summary>
    public AudioEditorWindow(string title, byte[] wav, Func<byte[], Task<bool>> apply, Func<G3MStudio.Core.Settings.AudioOptions>? audio = null, PcmAudio? decoded = null, Action<AudioEditorWindow>? generate = null)
    {
        _audio = decoded ?? AudioCodec.Decode(wav);
        _kind = SoundAudio.Format(wav) is "wav" ? "wav" : "ogg"; // an MP3 is stored again as OGG
        Title = title;
        Width = 560;
        MinWidth = 420;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _end.Value = (decimal)_audio.Seconds; // exact: a rounded value would cut the last milliseconds when nothing was changed
        _end.Maximum = (decimal)Math.Ceiling(_audio.Seconds * 100) / 100;
        _start.Maximum = _end.Maximum;
        foreach (var box in new[] { _start, _end, _gain, _fadeIn, _fadeOut }) box.ValueChanged += (_, _) => ShowLength();
        var applying = false;
        var ok = DialogFrame.Action(this, Loc.Current["audio.apply"], "AudioApply", async () =>
        {
            if (applying) return;
            applying = true;
            try
            {
                if (await apply(Result())) DialogFrame.Commit(this, () => Close(true)); // the click's own commit ended at the first await
                else _message.Text = Loc.Current["audio.notApplied"];
            }
            catch (InvalidOperationException e)
            {
                _message.Text = e.Message;
            }
            finally
            {
                applying = false;
            }
        }, DialogRole.Primary);
        var rows = new StackPanel { Spacing = 8 };
        Transport = new AudioTransport(_audio.Seconds, () => _audio, audio ?? (() => new()), () => (Seconds(_start), Seconds(_end)), "AudioPlay") { NoDevice = () => _message.Text = Loc.Current["audio.noDevice"] };
        foreach (var box in new[] { _start, _end }) box.ValueChanged += (_, _) => Transport.Redraw();
        Transport.RegionChanged += (from, to) => (_start.Value, _end.Value) = to > from ? ((decimal)from, (decimal)to) : (0m, (decimal)_audio.Seconds); // cleared: the whole sound
        Closed += (_, _) => Transport.Close();
        rows.Children.Add(Transport);
        rows.Children.Add(DialogFrame.Form((Loc.Current["audio.start"], _start), (Loc.Current["audio.end"], _end), (Loc.Current["audio.gain"], _gain), (Loc.Current["audio.fadeIn"], _fadeIn), (Loc.Current["audio.fadeOut"], _fadeOut)));
        rows.Children.Add(_info);
        _makers.IsVisible = generate is not null;
        if (generate is not null)
        {
            var make = new Button { Name = "AudioGenerate", Content = Loc.Current["cmd.edit.generateSound"] };
            ToolTip.SetTip(make, Loc.Current["synth.editorTip"]);
            make.Click += (_, _) => generate(this);
            _makers.Children.Add(make);
        }

        ToolTip.SetTip(_mic, Loc.Current["record.editorTip"]);
        _mic.Click += (_, _) => Record?.Invoke(this);
        _makers.Children.Add(_mic);
        rows.Children.Add(_makers);

        _info.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        ShowLength();
        DialogFrame.Show(this, rows, [ok, DialogFrame.Cancel(this, () => Close(false), "AudioCancel")], _message, DialogScroll.Vertical, DialogFrame.Snapshot(_start, _end, _gain, _fadeIn, _fadeOut));
    }

    /// <summary>Shows a button that opens the sound recorder on this sound.</summary>
    public Action<AudioEditorWindow>? Record
    {
        get;
        init
        {
            field = value;
            _mic.IsVisible = value is not null;
            _makers.IsVisible = _makers.Children.Any(c => c.IsVisible);
        }
    }

    private static NumericUpDown Field(string name, decimal value, decimal min, decimal max, decimal step, string format = "0.00") =>
        new() { Name = name, Value = value, Minimum = min, Maximum = max, Increment = step, FormatString = format, Width = 140 };

    private static double Seconds(NumericUpDown box) => (double)(box.Value ?? 0);

    // The edited audio is stored as the kind the sound already has (WAV or OGG); a WAV keeps its 16 bit samples, an OGG is encoded again.
    private byte[] Result() => AudioCodec.Encode(AudioCodec.Edit(_audio, Seconds(_start), Seconds(_end), Seconds(_gain) / 100, Seconds(_fadeIn), Seconds(_fadeOut)), _kind);

    private void ShowLength()
    {
        var length = Math.Max(0, Seconds(_end) - Seconds(_start));
        _info.Text = Loc.Current.Format("audio.length", ("seconds", length.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)));
        _message.Text = "";
    }
}
