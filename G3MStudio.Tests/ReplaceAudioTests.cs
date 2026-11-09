using G3MLib.DataFile;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ReplaceAudioTests
{
    private static byte[] Wav(int seconds)
    {
        var data = new byte[22050 * seconds];
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(36 + data.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(22050);
        writer.Write(22050);
        writer.Write((short)1);
        writer.Write((short)8);
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        return stream.ToArray();
    }

    [Fact]
    public void TheLengthOfAWavComesFromItsHeader()
    {
        Assert.Equal("wav", SoundAudio.Format(Wav(1)));
        Assert.Equal(3.0, SoundAudio.Seconds(Wav(3)));
        Assert.Null(SoundAudio.Seconds([1, 2, 3]));
        // A header cut off inside the Vorbis identification block is odd input, not an exception.
        Assert.Null(SoundAudio.Seconds([.. "OggS"u8, 0, 0, 0, 0, 0, 0, 0, 0, .. "vorbis"u8, 0, 0, 0, 0, 1, 0x44]));
    }

    [Fact]
    public async Task AudioIsReplacedByTheSameKindOfFileAndUndoes()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var data = await GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        var sound = data.Sounds.FirstOrDefault(s => s.AudioFile is { Data.Length: > 0 } a && SoundAudio.Format(a.Data) is "wav" or "ogg");
        Assert.False(sound is null, "The smallest corpus game has no embedded WAV or OGG sound.");
        var name = sound.Name.Content!;
        var kind = SoundAudio.Format(sound.AudioFile!.Data);
        var other = kind == "wav" ? Wav(2) : sound.AudioFile.Data[..^0];
        var (beforeData, beforeLength) = (sound.AudioFile.Data, sound.AudioLength);
        var command = EditCommand.Create(ReplaceAudioCommand.Name, new ReplaceAudioCommand(name, other).Args);
        command.Apply(data);
        Assert.Equal(other, sound.AudioFile.Data);
        if (kind == "wav") Assert.Equal(2f, sound.AudioLength);
        var wrong = kind == "wav" ? [(byte)'O', (byte)'g', (byte)'g', (byte)'S', 0, 0] : Wav(1);
        Assert.ThrowsAny<InvalidOperationException>(() => new ReplaceAudioCommand(name, wrong).Apply(data));
        command.Revert(data);
        Assert.Equal(beforeData, sound.AudioFile.Data);
        Assert.Equal(beforeLength, sound.AudioLength);
    }

    private static byte[] Tone16(int seconds)
    {
        const int rate = 8000;
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        var samples = rate * seconds;
        writer.Write("RIFF"u8);
        writer.Write(36 + samples * 2);
        writer.Write("WAVEfmt "u8);
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(rate);
        writer.Write(rate * 2);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write("data"u8);
        writer.Write(samples * 2);
        for (var i = 0; i < samples; i++) writer.Write((short)10000);
        return stream.ToArray();
    }

    [Fact]
    public void AWavIsCutScaledAndFaded()
    {
        var wav = Tone16(2);
        var cut = WavEdit.Edit(wav, 0.5, 1.5);
        Assert.Equal(1.0, WavEdit.Read(cut).Seconds, 3);
        Assert.Equal(1.0, SoundAudio.Seconds(cut)!.Value, 3);

        var quiet = WavEdit.Edit(wav, 0, 1, gain: 0.5);
        Assert.Equal(5000, BitConverter.ToInt16(quiet, WavEdit.Read(quiet).DataStart + 100 * 2));
        var loud = WavEdit.Edit(wav, 0, 1, gain: 16);
        Assert.Equal(short.MaxValue, BitConverter.ToInt16(loud, WavEdit.Read(loud).DataStart + 100 * 2)); // clipped, not wrapped

        var faded = WavEdit.Edit(wav, 0, 1, fadeIn: 0.5, fadeOut: 0.5);
        var start = WavEdit.Read(faded).DataStart;
        Assert.Equal(0, BitConverter.ToInt16(faded, start));
        Assert.InRange(BitConverter.ToInt16(faded, start + 2000 * 2), 4900, 5100); // half way into the fade in
        Assert.InRange(BitConverter.ToInt16(faded, start + 4000 * 2), 9900, 10000);
        Assert.Equal(0, BitConverter.ToInt16(faded, start + 7999 * 2));

        Assert.Throws<InvalidOperationException>(() => WavEdit.Edit(wav, 1, 1));
        Assert.Throws<InvalidOperationException>(() => WavEdit.Edit(wav, 0, 1, gain: -1));
        Assert.Throws<InvalidOperationException>(() => WavEdit.Read([1, 2, 3]));
    }

    [Fact]
    public async Task TheAudioEditorCutsAWavSoundAndUndoes()
    {
        var folder = Corpus.Folder;
        using var dir = new TempDir();
        string? source = null;
        foreach (var candidate in Corpus.Files(folder))
        {
            using var probe = await GameMakerIO.ReadAsync(candidate, cancellationToken: TestContext.Current.CancellationToken);
            if (probe.Sounds.Any(x => x.AudioFile is { Data.Length: > 2000 } a && SoundAudio.Format(a.Data) == "wav" && WavEdit.Read(a.Data).Seconds > 0.2)) { source = candidate; break; }
        }

        Assert.False(source is null, "No corpus game has an embedded WAV sound that the editor reads.");
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(source);
                var workspace = window.Workspaces.Current!;
                var sound = workspace.Data.Sounds.First(x => x.AudioFile is { Data.Length: > 2000 } a && SoundAudio.Format(a.Data) == "wav" && WavEdit.Read(a.Data).Seconds > 0.2);
                var before = sound.AudioFile!.Data.Length;
                window.Explorer.Select(ResourceTypes.KeyOf("sounds", sound.Name.Content!));
                AudioEditorWindow? editor = null;
                window.AudioEditorOpened += w => editor = w;
                Assert.True(window.Registry.Execute("edit.editAudio"));
                Assert.NotNull(editor);
                var end = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(editor).OfType<Avalonia.Controls.NumericUpDown>().Single(n => n.Name == "AudioEnd");
                end.Value = (end.Value ?? 0) / 2;
                Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(editor).OfType<Avalonia.Controls.Button>().Single(b => b.Name == "AudioApply").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.True(sound.AudioFile.Data.Length < before * 0.7, $"{sound.AudioFile.Data.Length} of {before}");
                workspace.History.Undo();
                Assert.Equal(before, sound.AudioFile.Data.Length);
            }
            finally
            {
                foreach (var open in window.OwnedWindows.ToList()) open.Close();
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ReplaceAudioWithAnOggFileConvertsItToTheKindTheSoundStores()
    {
        var folder = Corpus.Folder;
        using var dir = new TempDir();
        string? source = null;
        foreach (var candidate in Corpus.Files(folder))
        {
            using var probe = await GameMakerIO.ReadAsync(candidate, cancellationToken: TestContext.Current.CancellationToken);
            if (probe.Sounds.Any(x => x.AudioFile is { Data.Length: > 2000 } a && SoundAudio.Format(a.Data) == "wav")) { source = candidate; break; }
        }

        Assert.False(source is null, "No corpus game has an embedded WAV sound.");
        var oggPath = dir.File("replacement.ogg");
        var tone = new PcmAudio([.. Enumerable.Range(0, 22050 * 2).Select(i => (float)(Math.Sin(i / 2 * 0.1) * 0.4))], 2, 22050); // one second, stereo
        await File.WriteAllBytesAsync(oggPath, AudioCodec.Encode(tone, "ogg"), TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(source);
                var workspace = window.Workspaces.Current!;
                var sound = workspace.Data.Sounds.First(x => x.AudioFile is { Data.Length: > 2000 } a && SoundAudio.Format(a.Data) == "wav");
                window.FilePicker = (_, _) => Task.FromResult<string?>(oggPath);
                window.Explorer.Select(ResourceTypes.KeyOf("sounds", sound.Name.Content!));
                await window.ReplaceAudioAsync();
                Assert.Equal("wav", SoundAudio.Format(sound.AudioFile!.Data)); // stored as the kind it was
                Assert.InRange(WavEdit.Read(sound.AudioFile.Data).Seconds, 0.9, 1.1);
                workspace.History.Undo();
                Assert.NotEqual(1.0, Math.Round(WavEdit.Read(sound.AudioFile.Data).Seconds, 1));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TheAudioEditorCutsAnOggSoundAndStoresItAsOgg()
    {
        var folder = Corpus.Folder;
        var file = Corpus.Files(folder).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(file);
                var workspace = window.Workspaces.Current!;
                var sound = workspace.Data.Sounds.First(x => x.AudioFile is { Data.Length: > 100 });
                var tone = new PcmAudio([.. Enumerable.Range(0, 22050 * 2).Select(i => (float)(Math.Sin(i / 2 * 0.1) * 0.4))], 2, 22050);
                sound.AudioFile!.Data = AudioCodec.Encode(tone, "ogg"); // the sound now holds one second of OGG
                window.Explorer.Select(ResourceTypes.KeyOf("sounds", sound.Name.Content!));
                AudioEditorWindow? editor = null;
                window.AudioEditorOpened += w => editor = w;
                Assert.True(window.Registry.Execute("edit.editAudio"));
                Assert.NotNull(editor);
                var end = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(editor).OfType<Avalonia.Controls.NumericUpDown>().Single(n => n.Name == "AudioEnd");
                end.Value = (end.Value ?? 0) / 2;
                Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(editor).OfType<Avalonia.Controls.Button>().Single(b => b.Name == "AudioApply").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.Equal("ogg", SoundAudio.Format(sound.AudioFile.Data));
                Assert.InRange(AudioCodec.Decode(sound.AudioFile.Data).Seconds, 0.4, 0.62);
            }
            finally
            {
                foreach (var open in window.OwnedWindows.ToList()) open.Close();
                window.Close();
            }
        });
    }
}
