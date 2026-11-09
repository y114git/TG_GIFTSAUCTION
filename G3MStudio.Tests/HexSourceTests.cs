using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class HexSourceTests
{
    [Fact]
    public void ChunksAreFoundFromTheHeadersAndPointAtTheirNames()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1, withCode: true);
        using var source = new HexSource(path);
        var chunks = source.Chunks();
        Assert.Equal("GEN8", chunks[0].Name);
        Assert.Equal(8, chunks[0].Offset);
        Assert.Contains(chunks, c => c.Name == "CODE");
        foreach (var (name, offset) in chunks) Assert.Equal(name, System.Text.Encoding.ASCII.GetString(source.Read(offset, 4)));
        var plain = dir.File("plain.bin");
        File.WriteAllBytes(plain, new byte[64]);
        using var notForm = new HexSource(plain);
        Assert.Empty(notForm.Chunks());
    }

    [Fact]
    public void TheOffsetMapListsEveryChunkWithItsOffsetAndLength()
    {
        using var dir = new TempDir();
        using var source = new HexSource(TestData.WriteSample(dir, objects: 1, withCode: true));
        var lines = source.OffsetMap().Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(source.Chunks().Count, lines.Length);
        Assert.StartsWith("0x00000008 GEN8 ", lines[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RowsOffsetsAndSearchReadTheFileOnDemand()
    {
        using var dir = new TempDir();
        var path = dir.File("sample.bin");
        var bytes = new byte[70000];
        for (var i = 0; i < bytes.Length; i++) bytes[i] = (byte)(i % 251);
        "XYZW"u8.CopyTo(bytes.AsSpan(66000));
        await File.WriteAllBytesAsync(path, bytes, TestContext.Current.CancellationToken);
        using var source = new HexSource(path);
        Assert.Equal(70000, source.Length);
        Assert.Equal(4375, source.RowCount);
        Assert.Equal("00000000  00 01 02 03  04 05 06 07  08 09 0A 0B  0C 0D 0E 0F  ................", source.Row(0));
        Assert.Contains("XYZW", source.Row(66000 / 16)[^16..], StringComparison.Ordinal);
        Assert.Contains("58 59 5A 57", source.Row(66000 / 16), StringComparison.Ordinal);
        Assert.Equal(66000, source.Find("XYZW"u8.ToArray(), 0));
        Assert.Equal(-1, source.Find("XYZW"u8.ToArray(), 66001));
        Assert.Equal(66000, source.Find(HexSource.ParsePattern("58 59 5A 57")!, 100));
        Assert.Equal("XYZ"u8.ToArray(), HexSource.ParsePattern("XYZ"));
        Assert.Equal(source.Row(4374).Length, source.Row(4374).Length);
        Assert.True(HexSource.TryParseOffset("0x1A", out var hex) && hex == 26);
        Assert.True(HexSource.TryParseOffset("1_000", out var dec) && dec == 1000);
        Assert.False(HexSource.TryParseOffset("-5", out _));
        Assert.False(HexSource.TryParseOffset("zz", out _));
    }

    [Fact]
    public async Task TheHexWindowGoesToAnOffsetAndFindsText()
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
                Assert.True(window.Registry.Execute("tools.hexView"));
                var hex = window.OwnedWindows.OfType<HexViewWindow>().Single();
                var visuals = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(hex).ToList();
                var list = visuals.OfType<Avalonia.Controls.ListBox>().Single(l => l.Name == "HexRows");
                Assert.True(list.ItemCount > 1000);
                visuals.OfType<Avalonia.Controls.TextBox>().Single(t => t.Name == "HexGoTo").Text = "0x40";
                visuals.OfType<Avalonia.Controls.Button>().Single(b => b.Name == "HexGo").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.Equal(4, list.SelectedIndex);
                visuals.OfType<Avalonia.Controls.TextBox>().Single(t => t.Name == "HexFind").Text = "STRG";
                visuals.OfType<Avalonia.Controls.Button>().Single(b => b.Name == "HexNext").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
                Assert.True(list.SelectedIndex > 4); // the next STRG after row 4 is somewhere inside the file
                hex.Close();
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public async Task TheHexWindowOpensOnAFileAndFindsTextWithoutReadingItAll()
    {
        using var dir = new TempDir();
        var path = dir.File("big.bin");
        await using (var stream = File.Create(path))
        {
            stream.SetLength(40 * 1024 * 1024);
            stream.Position = 0;
            await stream.WriteAsync("FORM"u8.ToArray(), TestContext.Current.CancellationToken);
            stream.Position = 30 * 1024 * 1024;
            await stream.WriteAsync("NEEDLE"u8.ToArray(), TestContext.Current.CancellationToken);
        }

        await HeadlessApp.Run(async () =>
        {
            using var hex = new HexViewWindow(path);
            hex.Show();
            await Task.Delay(300);
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            var visuals = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(hex).ToList();
            var list = visuals.OfType<Avalonia.Controls.ListBox>().Single(l => l.Name == "HexRows");
            Assert.Equal(40 * 1024 * 1024 / 16, list.ItemCount);
            visuals.OfType<Avalonia.Controls.TextBox>().Single(t => t.Name == "HexFind").Text = "NEEDLE";
            visuals.OfType<Avalonia.Controls.Button>().Single(b => b.Name == "HexNext").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Avalonia.Controls.Button.ClickEvent));
            Assert.Equal(30 * 1024 * 1024 / 16, list.SelectedIndex);
            hex.Close();
        });
    }

    [Fact]
    public async Task AFileThatShrinksUnderTheViewGivesEmptyRowsNotAnException()
    {
        using var dir = new TempDir();
        var path = dir.File("shrinks.bin");
        await File.WriteAllBytesAsync(path, new byte[300000], TestContext.Current.CancellationToken);
        using var source = new HexSource(path);
        Assert.Contains("00 00", source.Row(10), StringComparison.Ordinal);
        await using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.ReadWrite)) stream.SetLength(1000);
        var row = source.Row(15000); // far beyond the new end, in a block that was never read
        Assert.StartsWith("0003A980", row, StringComparison.Ordinal);
        Assert.Equal(-1, source.Find([1, 2, 3], 200000));
    }
}
