using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>
/// The audio editor of a WAV or OGG sound: shows its waveform and cuts it to a start and end, scales its volume and fades its ends. Apply hands the new WAV
/// back; the caller makes the one undoable edit of the sound.
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
    private readonly TextBlock _info = new() { Name = "AudioInfo", Classes = { "muted" } };
    private readonly TextBlock _message = new() { Name = "AudioMessage", TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public AudioEditorWindow(string title, byte[] wav, Action<byte[]> apply)
    {
        _audio = AudioCodec.Decode(wav);
        _kind = SoundAudio.Format(wav) is "ogg" ? "ogg" : "wav";
        Title = title;
        Width = 560;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        _end.Value = (decimal)_audio.Seconds; // exact: a rounded value would cut the last milliseconds when nothing was changed
        _end.Maximum = (decimal)Math.Ceiling(_audio.Seconds * 100) / 100;
        _start.Maximum = _end.Maximum;
        foreach (var box in new[] { _start, _end, _gain, _fadeIn, _fadeOut }) box.ValueChanged += (_, _) => ShowLength();
        var ok = new Button { Name = "AudioApply", Content = Loc.Current["audio.apply"], Classes = { "primary" } };
        var cancel = new Button { Name = "AudioCancel", Content = Loc.Current["explorer.cancel"] };
        ok.Click += (_, _) =>
        {
            try
            {
                apply(Result());
                Close(true);
            }
            catch (InvalidOperationException e)
            {
                _message.Text = e.Message;
            }
        };
        cancel.Click += (_, _) => Close(false);
        var rows = new StackPanel { Spacing = 6 };
        rows.Children.Add(new WaveformView(WaveformView.Peaks(_audio)));
        rows.Children.Add(Row("audio.start", _start));
        rows.Children.Add(Row("audio.end", _end));
        rows.Children.Add(Row("audio.gain", _gain));
        rows.Children.Add(Row("audio.fadeIn", _fadeIn));
        rows.Children.Add(Row("audio.fadeOut", _fadeOut));
        rows.Children.Add(_info);
        rows.Children.Add(_message);
        rows.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Right, Children = { ok, cancel } });
        rows.Margin = new Avalonia.Thickness(16);
        Content = rows;
        ShowLength();
    }

    private static NumericUpDown Field(string name, decimal value, decimal min, decimal max, decimal step, string format = "0.00") =>
        new() { Name = name, Value = value, Minimum = min, Maximum = max, Increment = step, FormatString = format, Width = 140 };

    private static StackPanel Row(string key, Control field) =>
        new() { Orientation = Orientation.Horizontal, Spacing = 12, Children = { new TextBlock { Text = Loc.Current[key], Width = 200, VerticalAlignment = VerticalAlignment.Center }, field } };

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
