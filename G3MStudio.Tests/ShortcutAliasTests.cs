using Avalonia.Headless;
using Avalonia.Input;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using Window = Avalonia.Controls.Window;

namespace G3MStudio.Tests;

public sealed class ShortcutAliasTests
{
    [Fact]
    public void AliasesAreGesturesOfTheCommandAndSetAliasesReplacesThem()
    {
        var registry = AppCommands.Create(new FakeShell());
        Assert.Equal(["D", "B", "OemPeriod"], registry.Get("img.tool.pencil").Gestures);
        Assert.Equal(["B", "OemPeriod"], registry.DefaultAliases("img.tool.pencil"));
        var changes = new List<string>();
        registry.ShortcutChanged += changes.Add;
        registry.SetAliases("img.tool.pencil", ["N"]);
        registry.SetAliases("img.tool.pencil", ["N"]);
        Assert.Equal(["img.tool.pencil"], changes);
        Assert.Equal(["D", "N"], registry.Get("img.tool.pencil").Gestures);
        registry.SetAliases("img.tool.pencil", registry.DefaultAliases("img.tool.pencil"));
        Assert.Equal(["img.tool.pencil", "img.tool.pencil"], changes);
        Assert.Single(registry.UsersOf("I", "img.tool.pencil"));
        Assert.Empty(registry.UsersOf("I", "img.tool.picker"));
    }

    [Fact]
    public async Task GameMakerAliasesSelectTheToolsAndAReboundAliasReplacesThem()
    {
        await HeadlessApp.Run(() =>
        {
            var registry = AppCommands.Create(new FakeShell());
            using var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(8, 8))) { Bindings = id => registry.Find(id)?.Shortcut, AliasBindings = id => registry.Find(id)?.Aliases ?? [] };
            var window = new Window { Width = 1100, Height = 700, Content = view };
            window.Show();
            try
            {
                view.Canvas.Focus();
                window.KeyPress(Key.I, RawInputModifiers.None, PhysicalKey.None, null);
                Assert.Equal(ImageTool.Picker, view.Tool);
                window.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.None, null);
                Assert.Equal(ImageTool.Pencil, view.Tool);
                registry.SetAliases("img.tool.pencil", []);
                window.KeyPress(Key.I, RawInputModifiers.None, PhysicalKey.None, null);
                window.KeyPress(Key.B, RawInputModifiers.None, PhysicalKey.None, null);
                Assert.Equal(ImageTool.Picker, view.Tool);
            }
            finally { window.Close(); }

            return Task.CompletedTask;
        });
    }
}
