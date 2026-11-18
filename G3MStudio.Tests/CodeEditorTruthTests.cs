using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>A failed reload does not lock the text; the audio editor tells when the cut was not stored; WAVE_FORMAT_EXTENSIBLE and MP3 sounds open.</summary>
public sealed class CodeEditorTruthTests
{
    [Fact]
    public async Task AFailedReloadLeavesTheTextEditable()
    {
        using var dir = new TempDir();
        using var workspace = await CodeIntelUiTests.Open(dir);
        await HeadlessApp.Run(async () =>
        {
            var fail = false;
            var tab = new CodeEditorTab(workspace, "gml_Script_probe", () => fail ? throw new InvalidOperationException("gone") : new CodeLoad("x = 1;", CodeMode.Decompiled));
            var window = new Window { Content = tab, Width = 900, Height = 600 };
            window.Show();
            await tab.Ready;
            Assert.False(tab.Editor.IsReadOnly);
            tab.Editor.Text = "x = 2;";
            fail = true;
            tab.Revert();
            await tab.Ready;
            Assert.False(tab.Editor.IsReadOnly);
            Assert.Equal("x = 2;", tab.Editor.Text);
            window.Close();
        });
    }

    private static byte[] Extensible(int bits)
    {
        var data = new byte[bits / 8 * 100];
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write("RIFF"u8);
        writer.Write(4 + 8 + 40 + 8 + data.Length);
        writer.Write("WAVEfmt "u8);
        writer.Write(40);
        writer.Write((ushort)0xFFFE);
        writer.Write((ushort)1);
        writer.Write(8000);
        writer.Write(8000 * bits / 8);
        writer.Write((ushort)(bits / 8));
        writer.Write((ushort)bits);
        writer.Write((ushort)22);
        writer.Write((ushort)bits);
        writer.Write(4);
        writer.Write([0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x10, 0x00, 0x80, 0x00, 0x00, 0xAA, 0x00, 0x38, 0x9B, 0x71]);
        writer.Write("data"u8);
        writer.Write(data.Length);
        writer.Write(data);
        return stream.ToArray();
    }

    [Fact]
    public void ASixteenBitExtensibleWavReadsAndCuts()
    {
        var wav = Extensible(16);
        var format = WavEdit.Read(wav);
        Assert.Equal((1, 8000, 16, 100), (format.Channels, format.SampleRate, format.Bits, format.Frames));
        Assert.Equal(50, WavEdit.Read(WavEdit.Edit(wav, 0, 50.0 / 8000)).Frames);
        Assert.Equal(100, AudioCodec.Decode(wav).Frames);
    }

    [Fact]
    public void ATwentyFourBitExtensibleWavIsRefusedWithTheReason()
    {
        var e = Assert.Throws<InvalidOperationException>(() => WavEdit.Read(Extensible(24)));
        Assert.Contains("8 and 16 bit", e.Message, StringComparison.Ordinal);
    }

    private static Button Apply(AudioEditorWindow window) => window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "AudioApply");

    private static void Click(Button button) => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));

    [Fact]
    public async Task TheAudioEditorStaysOpenAndSaysSoWhenTheCutWasNotStored()
    {
        await HeadlessApp.Run(async () =>
        {
            var accept = false;
            var window = new AudioEditorWindow("Edit audio", ScreenshotTests.ToneWav(), _ => Task.FromResult(accept));
            window.Show();
            Click(Apply(window));
            await CodeIntelUiTests.Settle(() => window.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Name == "AudioMessage" && t.Text == Localization.Loc.Current["audio.notApplied"]));
            Assert.True(window.IsVisible);
            accept = true;
            Click(Apply(window));
            await CodeIntelUiTests.Settle(() => !window.IsVisible);
        });
    }

    [Fact]
    public async Task AnMp3SoundOpensInTheAudioEditorAndIsStoredAsOgg()
    {
        await HeadlessApp.Run(async () =>
        {
            byte[]? stored = null;
            var window = new AudioEditorWindow("Edit audio", AudioCodecTests.SilentMp3(), bytes =>
            {
                stored = bytes;
                return Task.FromResult(true);
            });
            window.Show();
            Click(Apply(window));
            await CodeIntelUiTests.Settle(() => stored is not null);
            Assert.Equal("ogg", SoundAudio.Format(stored!));
        });
    }
}
