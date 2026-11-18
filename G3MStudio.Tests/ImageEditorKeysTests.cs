using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Commands;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

/// <summary>With the focus inside the image editor, undo, redo, apply and the editing chords act on the picture and never on the data file.</summary>
public sealed class ImageEditorKeysTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255);

    private static void Stroke(ImageEditorView view, string points) => view.Canvas.Run(new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", points));

    private static void Press(Window window, Key key, RawInputModifiers? modifiers = null)
    {
        window.KeyPress(key, modifiers ?? HeadlessApp.Command, PhysicalKey.None, null);
        Dispatcher.UIThread.RunJobs();
    }

    [Fact]
    public async Task TheEditChordsActOnThePictureInAStandaloneEditor()
    {
        await HeadlessApp.Run(() =>
        {
            var session = new TargetImageSession(new PageTarget("Texture 0"), new PixelBuffer(16, 16));
            using var view = new ImageEditorView(session);
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                view.Canvas.Focus();
                var applied = 0;
                view.ApplyRequested += () => applied++;
                Press(window, Key.S);
                Assert.Equal(0, applied); // nothing to apply yet
                Stroke(view, "1,1 5,1");
                Assert.Equal(Red, view.Canvas.Picture.Get(3, 1));

                Press(window, Key.Z);
                Assert.Equal(Rgba.Clear, view.Canvas.Picture.Get(3, 1));
                Press(window, Key.Y);
                Assert.Equal(Red, view.Canvas.Picture.Get(3, 1));
                Press(window, Key.Z);
                Assert.Equal(Rgba.Clear, view.Canvas.Picture.Get(3, 1));
                Press(window, Key.Z, HeadlessApp.Command | RawInputModifiers.Shift);
                Assert.Equal(Red, view.Canvas.Picture.Get(3, 1));

                Press(window, Key.S);
                Assert.Equal(1, applied);

                Press(window, Key.A);
                Assert.NotNull(view.Canvas.State.Selection);
                Assert.Equal(new Box(0, 0, 16, 16), view.Canvas.State.Selection!.Bounds());
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ArrowsScrollTheViewWithoutASelectionAndNudgeWithOne()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(256, 128)));
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                view.Canvas.Focus();
                Press(window, Key.Left, RawInputModifiers.None); // the first layout pass centres the picture; measure after it
                var before = view.Canvas.Origin;
                Press(window, Key.Right, RawInputModifiers.None);
                Assert.Equal(before.X - 32, view.Canvas.Origin.X);
                Press(window, Key.Down, RawInputModifiers.None);
                Assert.Equal(before.Y - 32, view.Canvas.Origin.Y);

                view.Canvas.SetSelection(PixelSelection.Rectangle(256, 128, 10, 10, 12, 12));
                Press(window, Key.Right, RawInputModifiers.None);
                Assert.Equal(11, view.Canvas.State.Selection!.Bounds()!.Value.X); // with a selection the arrow moves its pixels, not the view
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task SpaceDragPansWhileAToolbarButtonHasTheFocusAndNeverPaintsOrClicksTheButton()
    {
        await HeadlessApp.Run(async () =>
        {
            using var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(256, 128)));
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                var edits = 0;
                view.Canvas.Edited += _ => edits++;
                await LayoutAudit.Settle(); // the bar places its groups once; a focused button must not be moved after that
                var fit = view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ImageFit");
                var clicks = 0;
                fit.Click += (_, _) => clicks++;
                fit.Focus();
                Dispatcher.UIThread.RunJobs();
                Assert.True(fit.IsFocused);

                var start = view.Canvas.TranslatePoint(new Point(200, 200), window)!.Value;
                window.MouseMove(start);
                var before = view.Canvas.Origin;
                window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                window.MouseDown(start, MouseButton.Left);
                window.MouseMove(start + new Vector(25, 10));
                window.MouseUp(start + new Vector(25, 10), MouseButton.Left);
                window.KeyRelease(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(before + new Vector(25, 10), view.Canvas.Origin);
                Assert.Equal(0, edits);
                Assert.Equal(0, clicks);

                // Space released: the drag draws again.
                window.MouseDown(start, MouseButton.Left);
                window.MouseUp(start, MouseButton.Left);
                Assert.Equal(1, edits);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task CopyCutPasteDeleteAndDuplicateRunAsImageCommandsFromTheEditor()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(16, 16)));
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                view.Canvas.Focus();
                Stroke(view, "0,0 7,0");
                Press(window, Key.X);
                Assert.Equal(Rgba.Clear, view.Canvas.Picture.Get(2, 0)); // cut cleared the whole picture (no selection)
                Press(window, Key.V);
                Assert.Equal(Red, view.Canvas.Picture.Get(2, 0));
                view.Canvas.SetSelection(PixelSelection.All(16, 16));
                Press(window, Key.Delete, RawInputModifiers.None);
                Assert.Equal(Rgba.Clear, view.Canvas.Picture.Get(2, 0));
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheMainWindowLeavesTheEditChordsToThePictureTabAndKeepsThemForTheFileElsewhere()
    {
        using var dir = new TempDir();
        string path;
        using (var data = CopyResourcesTests.Source()) path = TestData.Write(dir.File("data.win"), data);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                var asked = 0;
                window.SaveAsker = _ => { asked++; return Task.FromResult<(bool? Sync, bool Remember)?>((null, false)); };
                // One edit of the file itself, so the app-level undo has something to take back.
                workspace.Edit(ResourceTemplates.New(workspace.Data, "scripts", "scr_two"));
                var position = workspace.History.Position;
                Assert.True(workspace.History.CanUndo);

                window.Docking.OpenTab("image", "embeddedTextures:Texture 0", false);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var view = window.GetVisualDescendants().OfType<ImageEditorView>().Single();
                view.Canvas.Focus();
                Dispatcher.UIThread.RunJobs();
                Stroke(view, "1,1 5,1");
                Assert.Equal(Red, view.Canvas.Picture.Get(3, 1));
                Assert.True(view.Canvas.IsFocused, "focus " + window.FocusManager?.GetFocusedElement());

                Press(window, Key.Z);
                Assert.Equal(Rgba.Clear, view.Canvas.Picture.Get(3, 1));
                Assert.Equal(position, workspace.History.Position); // the file's own history was not touched
                Press(window, Key.Y);
                Assert.Equal(Red, view.Canvas.Picture.Get(3, 1));
                Assert.Equal(position, workspace.History.Position);
                Press(window, Key.Z);
                Assert.Equal(Rgba.Clear, view.Canvas.Picture.Get(3, 1));
                Press(window, Key.Z, HeadlessApp.Command | RawInputModifiers.Shift);
                Assert.Equal(Red, view.Canvas.Picture.Get(3, 1));
                Assert.Equal(position, workspace.History.Position);

                // Ctrl+S applies the picture (one more step of the file's history) and does not ask to save the file.
                Press(window, Key.S);
                await CodeIntelUiTests.Settle(() => workspace.History.Position == position + 1);
                Assert.Equal(position + 1, workspace.History.Position);
                Assert.Equal(0, asked);

                // Outside the editor Ctrl+Z is still the app's undo.
                window.Explorer.GetVisualDescendants().OfType<InputElement>().First(c => c.Focusable && c.IsEffectivelyVisible && c is not TextBox).Focus();
                Dispatcher.UIThread.RunJobs();
                Assert.False(view.Canvas.IsFocused);
                Press(window, Key.Z);
                Assert.Equal(position, workspace.History.Position);

                // Nothing left to apply: the same key saves the file, as everywhere else.
                view.Canvas.Focus();
                Dispatcher.UIThread.RunJobs();
                Press(window, Key.S);
                await CodeIntelUiTests.Settle(() => asked > 0);
                Assert.Equal(1, asked);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task CtrlSInASpriteEditorAppliesAWaitingPictureAndOnlyWithNoneSavesTheFile()
    {
        using var dir = new TempDir();
        string path;
        using (var data = CopyResourcesTests.Source()) path = TestData.Write(dir.File("data.win"), data);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var workspace = window.Workspaces.Current!;
                var asked = 0;
                window.SaveAsker = _ => { asked++; return Task.FromResult<(bool? Sync, bool Remember)?>((null, false)); };
                window.Docking.OpenTab("image", "embeddedTextures:Texture 0", false);
                Dispatcher.UIThread.RunJobs();
                var page = window.GetVisualDescendants().OfType<ImageEditorView>().Single();
                Stroke(page, "1,1 5,1"); // a page waits for Apply
                window.Docking.OpenTab("image", "sprites:spr_a", false);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var sprite = window.GetVisualDescendants().OfType<ImageEditorView>().Single(v => v != page && v.IsEffectivelyVisible);
                var position = workspace.History.Position;
                Stroke(sprite, "1,1 3,1"); // a sprite edit waits in its editor like the page one
                Assert.Equal(position, workspace.History.Position);
                sprite.Canvas.Focus();
                Dispatcher.UIThread.RunJobs();

                Press(window, Key.S); // this editor's own edit is applied first
                await CodeIntelUiTests.Settle(() => workspace.History.Position == position + 1);
                Assert.Equal(0, asked);

                Press(window, Key.S); // then the page waiting in the other tab
                await CodeIntelUiTests.Settle(() => workspace.History.Position == position + 2);
                Assert.Equal(0, asked); // applied the page and nothing else

                Press(window, Key.S);
                await CodeIntelUiTests.Settle(() => asked > 0);
                Assert.Equal(1, asked);
                Assert.Equal(position + 2, workspace.History.Position);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheButtonsNameTheirShortcutsInTheTooltips()
    {
        await HeadlessApp.Run(() =>
        {
            using var view = new ImageEditorView(new TargetImageSession(new PageTarget("Texture 0"), new PixelBuffer(16, 16)));
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                string Tip(string name) => ToolTip.GetTip(view.GetVisualDescendants().OfType<Button>().Single(b => b.Name == name))?.ToString() ?? "";
                Stroke(view, "1,1 5,1");
                Stroke(view, "1,3 5,3");
                view.Undo();
                Assert.Equal($"Undo ({Shortcuts.Display("Ctrl+Z")})", Tip("ImageUndo"));
                Assert.Equal($"Redo ({Shortcuts.Display("Ctrl+Y")})", Tip("ImageRedo"));
                Assert.Equal($"Apply ({Shortcuts.Display("Ctrl+S")})", Tip("ImageApply"));
                Assert.Equal("Fit (Alt+F)", Tip("ImageFit"));
                Assert.Equal("Actual Size (1:1) (Alt+C)", Tip("ImageActual"));
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }
}
