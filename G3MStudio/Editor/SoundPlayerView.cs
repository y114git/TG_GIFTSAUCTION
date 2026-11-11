using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// A sound above its properties with a player: play, pause, stop, seek bar, loop, a region to preview, volume and a waveform with a cursor, played inside Studio. When the
/// computer has no audio device, or the audio cannot be decoded, the bytes of the data file are written to a private temporary file and opened with the program the system
/// uses for that kind of file instead.
/// </summary>
public sealed class SoundPlayerView : StackPanel
{
    private readonly GameMakerSound? _sound;
    private readonly string _name;
    private readonly Func<byte[]> _bytes;
    private readonly Func<string, bool> _open;
    private readonly TextBlock _status = new() { Name = "SoundStatus", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    /// <summary>The player for bytes that are not a sound of the data file: an embedded audio entry or an audio file of the Included Files folder.</summary>
    public SoundPlayerView(string name, byte[] bytes, Func<string, bool> open, Func<AudioOptions>? audio = null) : this(null, name, () => bytes, open, audio)
    {
    }

    public SoundPlayerView(GameMakerSound sound, Func<string, bool> open, Func<AudioOptions>? audio = null) : this(sound, sound.Name?.Content ?? "sound", () => sound.AudioFile?.Data ?? [], open, audio)
    {
    }

    private SoundPlayerView(GameMakerSound? sound, string name, Func<byte[]> bytes, Func<string, bool> open, Func<AudioOptions>? audio)
    {
        (_sound, _name, _bytes, _open) = (sound, name, bytes, open);
        Name = "SoundPlayer";
        Spacing = 4;
        if (Bytes().Length == 0)
        {
            var play = new Button { Name = "PlaySound", Content = Loc.Current["sounds.play"], IsEnabled = false };
            Children.Add(play);
        }
        else
        {
            var start = Region("SoundRegionStart");
            var end = Region("SoundRegionEnd");
            Transport = new AudioTransport(Seconds() ?? 1, () => AudioCodec.Decode(Bytes()), WaveformView.Peaks(Bytes()), audio ?? (() => new AudioOptions()), () => ((double)(start.Value ?? 0), (double)(end.Value ?? 0)), "PlaySound") { NoDevice = Play };
            start.ValueChanged += (_, _) => Transport.Redraw();
            end.ValueChanged += (_, _) => Transport.Redraw();
            Children.Add(Transport);
            Children.Add(new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { new TextBlock { Text = Loc.Current["audio.region"], VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, start, end } });
        }

        Children.Add(_status);
        if (sound is not null)
        {
            Children.Add(new TextBlock { Name = "SoundOptions", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Text = Options });
            Children.Add(new TextBlock { Name = "SoundAudioRef", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Text = Reference });
        }
        _status.Text = Bytes().Length > 0
            ? Loc.Current.Format("sounds.info", ("kind", Extension()), ("kb", Bytes().Length / 1024), ("seconds", Seconds() is { } length ? Math.Round(length, 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : "?"))
            : Loc.Current.Format("sounds.outside", ("file", sound?.File?.Content ?? name));
    }

    /// <summary>The player, or null for a sound without audio in the data file.</summary>
    public AudioTransport? Transport { get; }

    private static NumericUpDown Region(string name) => new() { Name = name, Minimum = 0, Maximum = 100000, Increment = 0.05m, FormatString = "0.00", Value = 0, Width = 120 };

    /// <summary>Stops the sound and gives the device back (the tab closes).</summary>
    public void StopPlayback() => Transport?.Close();

    public string Status => _status.Text ?? "";

    /// <summary>Which audio entry the sound plays and from which audio group (UTMT-SND-03): the entry sits in this data file, or in the file of its group when the group is not the default one.</summary>
    public string Reference => _sound is null ? "" : _sound.AudioID < 0 ? Loc.Current["sounds.audioNone"] : Loc.Current.Format(_sound.AudioFile is null && _sound.GroupID != 0 ? "sounds.audioRefGroup" : "sounds.audioRef", ("id", _sound.AudioID), ("group", _sound.AudioGroup?.Name?.Content ?? _sound.GroupID.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Storage and compression of the sound in words: where the audio sits, its flags, volume and pitch.</summary>
    public string Options => _sound is null ? "" : Loc.Current.Format("sounds.options", ("flags", _sound.Flags.ToString()), ("volume", _sound.Volume.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)), ("pitch", _sound.Pitch.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));

    private byte[] Bytes() => _bytes();

    /// <summary>The length from the data file, or from the audio itself; null when neither tells (MP3 is not decoded).</summary>
    private double? Seconds() => _sound is { AudioLength: > 0 } ? _sound.AudioLength : SoundAudio.Seconds(Bytes());

    /// <summary>The file type the bytes announce: WAV, OGG or MP3 (nothing else is guessed).</summary>
    private string Extension() => SoundAudio.Format(Bytes());

    /// <summary>Writes the audio to a new private folder and returns the file, or null when the sound keeps its audio outside the data file.</summary>
    public string? Extract()
    {
        if (Bytes().Length == 0) return null;
        var folder = Directory.CreateTempSubdirectory("g3mstudio-audio-");
        var name = string.Concat(_name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(folder.FullName, name + "." + Extension());
        File.WriteAllBytes(path, Bytes());
        return path;
    }

    private void Play()
    {
        var path = Extract();
        if (path is null || !_open(path)) _status.Text = Loc.Current.Format("sounds.noPlayer", ("path", path ?? ""));
    }
}
