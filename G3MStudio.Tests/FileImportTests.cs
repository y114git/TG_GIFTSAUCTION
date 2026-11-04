using G3MStudio.Core.Workspace;
using ImageMagick;

namespace G3MStudio.Tests;

public sealed class FileImportTests
{
    [Fact]
    public void APictureAWavAndAGmlFileBecomeASpriteASoundAndAScript()
    {
        using var dir = new TempDir();
        using var data = TestData.Create();
        using (var image = new MagickImage(MagickColors.Red, 40, 24)) image.Write(dir.File("hero one.png"), MagickFormat.Png);
        File.WriteAllBytes(dir.File("beep.wav"), WavEdit.Silence(1));
        File.WriteAllText(dir.File("obj_0.gml"), "return 7;");

        FileImport.Build(data, dir.File("hero one.png"), out var type, out var name);
        Assert.Equal(("sprites", "hero_one"), (type, name));
        FileImport.Build(data, dir.File("beep.wav"), out _, out _).Apply(data);
        Assert.Contains(data.Sounds, s => s.Name.Content == "beep");

        // obj_0 is taken by an object, so the script gets a number.
        FileImport.Build(data, dir.File("obj_0.gml"), out _, out var script).Apply(data);
        Assert.Equal("obj_0_1", script);
        Assert.Contains(data.Code, c => c.Name.Content == "gml_Script_obj_0_1");

        File.WriteAllText(dir.File("notes.txt"), "x");
        Assert.Null(FileImport.TypeOf(dir.File("notes.txt")));
        File.WriteAllText(dir.File("bad.wav"), "not audio");
        var bad = dir.File("bad.wav");
        Assert.Throws<InvalidDataException>(() => FileImport.Build(data, bad, out _, out _));
    }

    [Fact]
    public async Task ASpriteFromAPictureHasThePictureSizeInAGame()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || !Directory.Exists(folder), "G3MSTUDIO_CORPUS_DIR is not set");
        var file = Directory.EnumerateFiles(folder, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        using var data = await G3MLib.DataFile.GameMakerIO.ReadAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        using (var image = new MagickImage(MagickColors.Red, 40, 24)) await image.WriteAsync(dir.File("hero.png"), MagickFormat.Png, TestContext.Current.CancellationToken);
        FileImport.Build(data, dir.File("hero.png"), out _, out var name).Apply(data);
        var made = data.Sprites.Single(s => s.Name.Content == name);
        Assert.Equal((40, 24), ((int)made.Width, (int)made.Height));
        Assert.Equal(24, made.Textures[0].Texture!.TargetHeight);
    }

    [Fact]
    public void AnOggFileMakesACompressedOggSoundAndUndoes()
    {
        using var dir = new TempDir();
        using var data = TestData.Create();
        File.WriteAllBytes(dir.File("tune.ogg"), AudioCodec.Convert(WavEdit.Silence(1), "ogg"));

        var import = FileImport.Build(data, dir.File("tune.ogg"), out var type, out var name);
        Assert.Equal(("sounds", "tune"), (type, name));
        import.Apply(data);
        var sound = data.Sounds.Single(s => s.Name.Content == "tune");
        Assert.Equal(".ogg", sound.Type!.Content);
        Assert.Equal("tune.ogg", sound.File!.Content);
        Assert.True(sound.Flags.HasFlag(G3MLib.DataFile.Models.GameMakerSound.AudioEntryFlags.IsCompressed));
        Assert.Equal("ogg", SoundAudio.Format(sound.AudioFile!.Data));

        import.Revert(data);
        Assert.DoesNotContain(data.Sounds, s => s.Name.Content == "tune");
    }
}