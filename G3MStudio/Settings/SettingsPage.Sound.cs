using Avalonia.Controls;
using G3MStudio.Audio;
using G3MStudio.Core.Settings;
using G3MStudio.Localization;

namespace G3MStudio.Settings;

public sealed partial class SettingsPage
{
    /// <summary>Playback inside Studio (sound view and audio editor): backend, output device, start volume and buffer size.</summary>
    private Control[] SoundSection(AppSettings s)
    {
        var backends = AudioDevices.Backends;
        var backend = AudioDevices.BackendOf(s.Audio);
        var devices = backend.List().ToList();
        if (s.Audio.Device.Length > 0 && !devices.Contains(s.Audio.Device)) devices.Add(s.Audio.Device); // a device that is unplugged now stays chosen
        devices.Insert(0, "");
        var device = new ComboBox { Name = "AudioDevice", ItemsSource = devices.Select(d => d.Length == 0 ? Loc.Current["settings.default"] : d).ToList(), SelectedIndex = devices.IndexOf(s.Audio.Device), MinWidth = Wide(15) };
        device.SelectionChanged += (_, _) =>
        {
            if (!_building && device.SelectedIndex >= 0) _store.Update(x => x with { Audio = x.Audio with { Device = devices[device.SelectedIndex] } });
        };
        var picker = new ComboBox { Name = "AudioBackend", ItemsSource = backends.Select(b => Loc.Current[b.LabelKey]).ToList(), SelectedIndex = backends.IndexOf(backend), MinWidth = Wide(15) };
        picker.SelectionChanged += (_, _) =>
        {
            if (_building || picker.SelectedIndex < 0 || backends[picker.SelectedIndex].Id == _store.Current.Audio.Backend) return;
            _store.Update(x => x with { Audio = x.Audio with { Backend = backends[picker.SelectedIndex].Id } });
            Content = Build();
        };
        var note = new TextBlock { Name = "AudioBackendNote", Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Text = Loc.Current.Format("audio.noLibrary", ("file", AudioDevices.LibraryFile)) };
        SetSelfHidden(note, backend.Available());
        return
        [
            Heading("settings.sound"),
            Row("settings.soundBackend", picker),
            note,
            Row("settings.soundDevice", device),
            Row("settings.soundVolume", Number("AudioVolumeSetting", 0, 100, 5, s.Audio.Volume, (x, n) => x with { Audio = x.Audio with { Volume = n } })),
            Row("settings.soundBuffer", Number("AudioBufferSetting", AudioOptions.MinBufferMs, AudioOptions.MaxBufferMs, 10, s.Audio.BufferMs, (x, n) => x with { Audio = x.Audio with { BufferMs = n } })),
        ];
    }
}
