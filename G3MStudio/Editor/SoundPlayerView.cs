using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// A sound above its properties with a Play button. The audio bytes of the data file are written to a private temporary file and opened with
/// the program the system uses for that kind of file, so playback needs no audio library and works the same on every platform.
/// </summary>
public sealed class SoundPlayerView : StackPanel
{
    private readonly GameMakerSound _sound;
    private readonly Func<string, bool> _open;
    private readonly TextBlock _status = new() { Name = "SoundStatus", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap };

    public SoundPlayerView(GameMakerSound sound, Func<string, bool> open)
    {
        (_sound, _open) = (sound, open);
        Name = "SoundPlayer";
        Spacing = 4;
        var play = new Button { Name = "PlaySound", Content = Loc.Current["sounds.play"], IsEnabled = Bytes().Length > 0 };
        play.Click += (_, _) => Play();
        Children.Add(play);
        Children.Add(_status);
        _status.Text = Bytes().Length > 0
            ? Loc.Current.Format("sounds.info", ("kind", Extension()), ("kb", Bytes().Length / 1024), ("seconds", Math.Round(sound.AudioLength, 1)))
            : Loc.Current.Format("sounds.outside", ("file", sound.File?.Content ?? ""));
    }

    public string Status => _status.Text ?? "";

    private byte[] Bytes() => _sound.AudioFile?.Data ?? [];

    /// <summary>The file type the bytes announce: WAV, OGG or MP3 (nothing else is guessed).</summary>
    private string Extension() => Bytes() switch
    {
        [(byte)'R', (byte)'I', (byte)'F', (byte)'F', ..] => "wav",
        [(byte)'O', (byte)'g', (byte)'g', (byte)'S', ..] => "ogg",
        [0x49, 0x44, 0x33, ..] or [0xFF, ..] => "mp3",
        _ => "bin",
    };

    /// <summary>Writes the audio to a new private folder and returns the file, or null when the sound keeps its audio outside the data file.</summary>
    public string? Extract()
    {
        if (Bytes().Length == 0) return null;
        var folder = Directory.CreateTempSubdirectory("g3mstudio-audio-");
        var name = string.Concat((_sound.Name?.Content ?? "sound").Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
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
