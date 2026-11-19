using Avalonia;
using Avalonia.Headless;
using Avalonia.Input;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using Window = Avalonia.Controls.Window;

namespace G3MStudio.Tests;

public sealed class MouseGestureTests
{
    [Fact]
    public void TheBackAndForwardButtonsWalkTheResourcesNextToTheKeysAndAnyButtonCanBeBound()
    {
        var registry = AppCommands.Create(new FakeShell());
        Assert.Equal(["Alt+Left"], registry.Get("tabs.back").Gestures);
        Assert.Equal("tabs.back", MouseGestures.Match(registry, "Mouse4")?.Id);
        Assert.Equal("tabs.forward", MouseGestures.Match(registry, "Mouse5")?.Id);
        Assert.Null(MouseGestures.Match(registry, "Mouse3"));

        registry.SetShortcut("tabs.back", null);
        Assert.Equal("tabs.back", MouseGestures.Match(registry, "Mouse4")?.Id);
        registry.SetShortcut("tabs.historyBack", "Ctrl+Mouse4");
        registry.SetAliases("tabs.historyForward", ["Mouse3", "Ctrl+Mouse1"]);
        Assert.Equal("tabs.historyBack", MouseGestures.Match(registry, "ctrl+mouse4")?.Id);
        Assert.Equal("tabs.historyForward", MouseGestures.Match(registry, "Mouse3")?.Id);
        Assert.Equal("tabs.historyForward", MouseGestures.Match(registry, "Ctrl+Mouse1")?.Id);
    }

    [Fact]
    public void AGestureTheUserChoseBeatsADefaultAndOnlyMouseTextCounts()
    {
        var registry = AppCommands.Create(new FakeShell());
        registry.SetShortcut("tabs.pin", "Mouse4");
        Assert.Equal("tabs.pin", MouseGestures.Match(registry, "Mouse4")?.Id);
        Assert.Equal("tabs.forward", MouseGestures.Match(registry, "Mouse5")?.Id);
        Assert.Equal("Alt+Mouse 5", Shortcuts.Display("Alt+Mouse5"));
    }

    [Theory]
    [InlineData("Mouse3", true)]
    [InlineData("Mouse4", true)]
    [InlineData("Alt+Mouse5", true)]
    [InlineData("Ctrl+Shift+Mouse4", true)]
    [InlineData("Ctrl+S", false)]
    [InlineData("Mouse0", false)]
    [InlineData("Mouse1", false)]
    [InlineData("mouse2", false)]
    [InlineData("Ctrl+Mouse1", true)]
    [InlineData("Mouse6", false)]
    [InlineData("Foo+Mouse4", false)]
    [InlineData(null, false)]
    public void OnlyTextNamingAMouseButtonWithKnownModifiersIsAMouseGesture(string? gesture, bool expected) => Assert.Equal(expected, MouseGestures.Is(gesture));

    [Theory]
    [InlineData(MouseButton.XButton1, RawInputModifiers.None, "Mouse4")]
    [InlineData(MouseButton.XButton2, RawInputModifiers.Control | RawInputModifiers.Shift, "Ctrl+Shift+Mouse5")]
    [InlineData(MouseButton.Middle, RawInputModifiers.Alt, "Alt+Mouse3")]
    [InlineData(MouseButton.Left, RawInputModifiers.Control, "Ctrl+Mouse1")]
    [InlineData(MouseButton.Right, RawInputModifiers.Shift, "Shift+Mouse2")]
    [InlineData(MouseButton.Left, RawInputModifiers.None, null)]
    [InlineData(MouseButton.Right, RawInputModifiers.None, null)]
    public async Task APressedButtonIsDescribedLikeAGestureAndABareLeftOrRightClickIsNot(MouseButton button, RawInputModifiers modifiers, string? expected)
    {
        await HeadlessApp.Run(() =>
        {
            var seen = new List<string?>();
            var window = new Window { Width = 200, Height = 100 };
            window.AddHandler(InputElement.PointerPressedEvent, (_, e) => seen.Add(MouseGestures.Describe(e)), Avalonia.Interactivity.RoutingStrategies.Tunnel);
            window.Show();
            try
            {
                window.MouseDown(new Point(50, 50), button, modifiers);
                window.MouseUp(new Point(50, 50), button, modifiers);
                Assert.Equal([expected], seen);
            }
            finally
            {
                window.Close();
            }

            return Task.CompletedTask;
        });
    }
}
