using G3MLib.DataFile;
using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

#pragma warning disable CA2000 // the data model owns what is added to it

namespace G3MStudio.Tests;

/// <summary>The main window entry points of the image editor, the text editor and the sound player.</summary>
public sealed class ImageEntryPointWindowTests
{
    private static byte[] Wav(int seconds) => AudioCodec.Encode(new PcmAudio([.. Enumerable.Range(0, seconds * 8000).Select(i => i / (float)(seconds * 8000))], 1, 8000), "wav");

    private static string WriteSource(TempDir dir)
    {
        using var data = CopyResourcesTests.Source();
        return TestData.Write(dir.File("data.win"), data);
    }

    [Fact]
    public async Task TheExplorerMenuOffersEditImageOnPictureRowsAndItOpensTheImageTab()
    {
        using var dir = new TempDir();
        var path = WriteSource(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var edit = Assert.Single(window.ResourceMenuFor("sprites:spr_a").OfType<MenuItem>(), i => i.Header as string == "Edit Image...");
                Assert.DoesNotContain(window.ResourceMenuFor("objects:obj_a").OfType<MenuItem>(), i => i.Header as string == "Edit Image...");
                edit.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(MenuItem.ClickEvent));
                Assert.Contains(window.Docking.Tabs.Tabs, t => t is { Kind: "image", Key: "sprites:spr_a" });
                window.EditImageKey("objects:obj_a");
                Assert.DoesNotContain(window.Docking.Tabs.Tabs, t => t.Key == "objects:obj_a" && t.Kind == "image");
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ATextFileOpensInTheEditorControlAndItsCommandWritesItBackUndoably()
    {
        using var dir = new TempDir();
        var path = WriteSource(dir);
        await File.WriteAllTextAsync(dir.File("notes.txt"), "one", TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                Assert.True(window.OpenIncludedFile("notes.txt"));
                Assert.False(window.OpenIncludedFile("lib.dll"));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var view = window.GetVisualDescendants().OfType<TextFileView>().Single();
                Assert.Equal("one", view.Text);
                view.SetText("two");
                Assert.True(view.HasPending);
                var workspace = window.Workspaces.Current!;
                workspace.Edit(new IncludedTextCommand(workspace.Path, "notes.txt", "two", true));
                Assert.Equal("two", await File.ReadAllTextAsync(dir.File("notes.txt"), TestContext.Current.CancellationToken));
                workspace.Undo();
                Assert.Equal("one", await File.ReadAllTextAsync(dir.File("notes.txt"), TestContext.Current.CancellationToken));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task EmbeddedAndIncludedAudioOpenInTheSoundPlayerAndReplaceUndoably()
    {
        using var dir = new TempDir();
        using (var data = CopyResourcesTests.Source())
        {
            data.EmbeddedAudio.Add(new GameMakerEmbeddedAudio { Name = data.Strings.MakeString("audio0"), Data = Wav(1) });
            TestData.Write(dir.File("data.win"), data);
        }

        await File.WriteAllBytesAsync(dir.File("hit.ogg"), [7, 7], TestContext.Current.CancellationToken);
        await File.WriteAllBytesAsync(dir.File("new.wav"), Wav(2), TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(dir.File("data.win"));
                var workspace = window.Workspaces.Current!;
                var entry = Assert.Single(workspace.Data.EmbeddedAudio).Name.Content!; // a data file names its entries again when it is read
                window.Docking.OpenTab("audio", "embeddedAudio:" + entry, false);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                Assert.Contains(window.GetVisualDescendants().OfType<Button>(), b => b.Name == "AudioFileReplace");
                await window.ReplaceAudioBytesFromAsync(workspace, "embeddedAudio", entry, dir.File("new.wav"));
                Assert.Equal(Wav(2).Length, MainWindow.AudioBytes(workspace, "embeddedAudio", entry)!.Length);
                workspace.Undo();
                Assert.Equal(Wav(1).Length, MainWindow.AudioBytes(workspace, "embeddedAudio", entry)!.Length);
                Assert.True(window.OpenIncludedFile("hit.ogg"));
                Assert.Equal([7, 7], MainWindow.AudioBytes(workspace, "includedFiles", "hit.ogg"));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ADroppedPictureReplacesTheFrameThroughTheTargetInOneUndoStep()
    {
        using var dir = new TempDir();
        var path = WriteSource(dir);
        using (var png = new ImageMagick.MagickImage(ImageMagick.MagickColors.Red, 4, 4)) await File.WriteAllBytesAsync(dir.File("drop.png"), png.ToByteArray(ImageMagick.MagickFormat.Png), TestContext.Current.CancellationToken);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                window.Docking.OpenTab("image", "sprites:spr_a", false);
                var red = new Rgba(255, 0, 0, 255);
                await window.ReplacePictureFromAsync(workspace, "sprites:spr_a", dir.File("drop.png"));
                Assert.Equal(red, new FrameTarget("spr_a", 0).Read(workspace.Data).Get(1, 1));
                workspace.Undo();
                Assert.NotEqual(red, new FrameTarget("spr_a", 0).Read(workspace.Data).Get(1, 1));
            }
            finally { window.Close(); }
        });
    }
}
