using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

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
    private readonly byte[] _loaded;
    private readonly ExternalAudio? _external;
    private readonly ExternalSource? _source;
    private volatile byte[]? _externalBytes; // read on a pool thread by the decode of the player, then used for the system player and the file type
    private readonly Func<string, bool> _open;
    private readonly TextBlock _status = new() { Name = "SoundStatus", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    /// <summary>The player for bytes that are not a sound of the data file: an embedded audio entry or an audio file of the Included Files folder.</summary>
    public SoundPlayerView(string name, byte[] bytes, Func<string, bool> open, Func<AudioOptions>? audio = null) : this(null, name, () => bytes, open, audio)
    {
    }

    /// <summary>The player for a sound of the data file. With <paramref name="external"/> a sound whose audio is in an audio group file or a streamed file beside the data file plays from there, read only; without it such a sound has no player.</summary>
    public SoundPlayerView(GameMakerSound sound, Func<string, bool> open, Func<AudioOptions>? audio = null, ExternalAudio? external = null) : this(sound, sound.Name?.Content ?? "sound", () => sound.AudioFile?.Data ?? [], open, audio, external)
    {
    }

    private SoundPlayerView(GameMakerSound? sound, string name, Func<byte[]> bytes, Func<string, bool> open, Func<AudioOptions>? audio, ExternalAudio? external = null)
    {
        (_sound, _name, _bytes, _open, _external) = (sound, name, bytes, open, external);
        _loaded = bytes();
        Name = "SoundPlayer";
        Spacing = 4;
        if (_loaded.Length == 0 && sound is not null) _source = external?.Locate(sound);
        if (sound is not null && _source is { Exists: true })
        {
            // The audio is read and decoded on a pool thread when the player shows, so a long file does not hold the window.
            var player = new RegionPlayer(sound.AudioLength > 0 ? sound.AudioLength : 1, () => AudioCodec.Decode(ReadExternal()), audio ?? (() => new AudioOptions()), "PlaySound");
            Transport = player.Transport;
            Transport.NoDevice = () => _ = Play();
            Children.Add(player);
        }
        else if (Bytes().Length == 0)
        {
            var play = new Button { Name = "PlaySound", Content = Loc.Current["sounds.play"] }.GateKey("sounds.noAudioData");
            Children.Add(play);
        }
        else
        {
            var player = new RegionPlayer(Seconds() ?? 1, () => AudioCodec.Decode(Bytes()), audio ?? (() => new AudioOptions()), "PlaySound");
            Transport = player.Transport;
            Transport.NoDevice = () => _ = Play();
            Children.Add(player);
        }

        Children.Add(_status);
        if (sound is not null)
        {
            Children.Add(new TextBlock { Name = "SoundOptions", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Text = Options });
            Children.Add(new TextBlock { Name = "SoundAudioRef", Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Text = Reference });
        }
        _status.Text = Bytes().Length > 0
            ? Loc.Current.Format("sounds.info", ("kind", Extension().ToUpperInvariant()), ("kb", Bytes().Length / 1024), ("seconds", Seconds() is { } length ? Math.Round(length, 1).ToString(System.Globalization.CultureInfo.InvariantCulture) : "?"))
            : _source is { Exists: true } found ? Loc.Current.Format("sounds.outsidePlays", ("file", found.Path))
            : Loc.Current.Format("sounds.outside", ("file", _source?.Path ?? sound?.File?.Content ?? name));
        if (Bytes().Length == 0 && _source is not { Exists: true }) HelpText.SetKind(_status, HelpKind.None); // why nothing plays, whatever the explanatory text setting
    }

    /// <summary>The player, or null for a sound without audio in the data file.</summary>
    public AudioTransport? Transport { get; }

    /// <summary>Stops the sound and gives the device back (the tab closes).</summary>
    public void StopPlayback() => Transport?.Close();

    public string Status => _status.Text ?? "";

    /// <summary>Which audio entry the sound plays and from which audio group (UTMT-SND-03): the entry sits in this data file, or in the file of its group when the group is not the default one.</summary>
    public string Reference => _sound is null ? "" : _sound.AudioID < 0 ? Loc.Current["sounds.audioNone"] : Loc.Current.Format(_sound.AudioFile is null && _sound.GroupID != 0 ? "sounds.audioRefGroup" : "sounds.audioRef", ("id", _sound.AudioID), ("group", _sound.AudioGroup?.Name?.Content ?? _sound.GroupID.ToString(System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>Storage and compression of the sound in words: where the audio sits, its flags, volume and pitch.</summary>
    public string Options => _sound is null ? "" : Loc.Current.Format("sounds.options", ("flags", _sound.Flags.ToString()), ("volume", _sound.Volume.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)), ("pitch", _sound.Pitch.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)));

    /// <summary>True once the sound holds other audio than this view was made for (Replace Audio, the recorder, Undo): the view shows the old length and size until it is built again.</summary>
    public bool IsStale => !ReferenceEquals(_loaded, Bytes()) && (_loaded.Length > 0 || Bytes().Length > 0);

    private byte[] Bytes() => _bytes();

    // The audio from outside the data file, read once.
    private byte[] ReadExternal() => _externalBytes ??= (_external is { } reader && _sound is { } sound ? reader.Load(sound) : null) ?? throw new InvalidDataException("The audio file is not there.");

    /// <summary>The length from the data file, or from the audio itself; null when neither tells (MP3 is not decoded).</summary>
    private double? Seconds() => _sound is { AudioLength: > 0 } ? _sound.AudioLength : SoundAudio.Seconds(Bytes());

    /// <summary>The file type the bytes announce: WAV, OGG or MP3 (nothing else is guessed).</summary>
    private string Extension() => SoundAudio.Format(Audio());

    // The audio to play: the one of the data file, else what was read from beside it.
    private byte[] Audio() => Bytes().Length > 0 ? Bytes() : _externalBytes ?? [];

    /// <summary>Writes the audio to a new private folder and returns the file, or null when the sound keeps its audio outside the data file.</summary>
    public async Task<string?> ExtractAsync()
    {
        if (Audio().Length == 0) return null;
        var folder = Directory.CreateTempSubdirectory("g3mstudio-audio-");
        var name = string.Concat(_name.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var path = Path.Combine(folder.FullName, name + "." + Extension());
        await File.WriteAllBytesAsync(path, Audio());
        return path;
    }

    private async Task Play()
    {
        var path = await ExtractAsync();
        if (path is not null && _open(path)) return;
        HelpText.SetKind(_status, HelpKind.None); // a failed play says why, whatever the explanatory text setting
        _status.Text = path is null && _source is not null ? Loc.Current.Format("sounds.outside", ("file", _source.Path)) : Loc.Current.Format("sounds.noPlayer", ("path", path ?? ""));
    }
}
