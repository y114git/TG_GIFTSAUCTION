using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using G3MStudio.Rooms;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

#pragma warning disable CA2000 // the windows are closed by the test
namespace G3MStudio.Tests;

/// <summary>Dragging the canvas size handles of the sprite editor while the preview plays: bounded work per move, nothing escapes the pointer event.</summary>
public sealed class SpriteCanvasDragTests
{
    [Fact]
    public async Task DraggingFarOutWhileThePreviewPlaysStaysFast()
    {
        using var dir = new TempDir();
        string file;
        using (var data = TestData.Create(0))
        {
            TestData.AddPage(data);
            SpriteTemplate.Create(data, "spr_edit");
            file = TestData.Write(dir.File("data.win"), data);
        }

        var userData = new UserData(dir.File("user"));
        using var workspace = await new WorkspaceService(userData, RecentFiles.Load(userData)).OpenAsync(file, cancellationToken: TestContext.Current.CancellationToken);
        for (var i = 0; i < 7; i++) workspace.Edit(new FrameListCommand("spr_edit", FrameListCommand.Duplicate, 0));
        var sprite = workspace.Data.Sprites.First(x => x.Name.Content == "spr_edit");
        await HeadlessApp.Run(async () =>
        {
            using var textures = new TextureBitmaps();
            var editor = new SpriteEditorView(sprite, textures, workspace);
            var window = new Window { Width = 1000, Height = 700, Content = editor };
            window.Show();
            await editor.Tools.Ready;
            Dispatcher.UIThread.RunJobs();
            var canvas = editor.Tools.Canvas;
            canvas.AllFrames = true;
            var right = canvas.TranslatePoint(new Point(SpriteCanvas.Pad + (sprite.Width * canvas.Zoom) + 12, SpriteCanvas.Pad + (sprite.Height * canvas.Zoom) + 12), window)!.Value;
            window.MouseMove(right);
            window.MouseDown(right, MouseButton.Left);
            var watch = Stopwatch.StartNew();
            for (var i = 1; i <= 40; i++)
            {
                window.MouseMove(right + new Vector(i * 1000, i * 1000), RawInputModifiers.LeftMouseButton);
                editor.Frames.Step();
                Dispatcher.UIThread.RunJobs();
                window.CaptureRenderedFrame();
            }

            var spent = watch.ElapsedMilliseconds;
            var up = Stopwatch.StartNew();
            window.MouseUp(right + new Vector(40000, 40000), MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            await editor.Tools.Ready;
            Dispatcher.UIThread.RunJobs();
            window.CaptureRenderedFrame();
            var upMs = up.ElapsedMilliseconds;
            Assert.True(spent < 5000 && upMs < 15000, $"moves {spent} ms, drop {upMs} ms");
            Assert.True(canvas.Zoom * Math.Max(sprite.Width, sprite.Height) <= SpriteCanvas.MaxExtent, $"zoom {canvas.Zoom} size {sprite.Width}");
            window.Close();
        });
    }
}
