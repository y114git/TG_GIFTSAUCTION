using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Dock.Avalonia.Controls;
using G3MStudio.Docking;

namespace G3MStudio.Tests;

/// <summary>A button inside a tab, a list row or a panel header must change visibly under the pointer: its hover and pressed fill differs from the fill of the parent in every state of the parent.</summary>
public sealed class HoverContrastTests
{
    // Hover and pressed fills are weak on purpose; the tab close button, which sits flat on the tab, has to stand out more.
    private const double Visible = 1.5;
    private const double Strong = 2.0;

    private static readonly string[] PointerStates = [":pointerover", ":pressed"];
    // A press on a button inside a row does not press the row, so the row is plain, hovered, chosen or both.
    private static readonly string[] RowStates = ["", ":pointerover", ":selected", ":selected:pointerover"];
    private static readonly string[] RowFlags = [":pointerover", ":selected"];

    private static Color Over(Color top, Color back)
    {
        var a = top.A / 255.0;
        static byte Mix(byte t, byte b, double a) => (byte)Math.Round((t * a) + (b * (1 - a)));
        return Color.FromRgb(Mix(top.R, back.R, a), Mix(top.G, back.G, a), Mix(top.B, back.B, a));
    }

    private static double Luminance(Color c)
    {
        static double Channel(byte v)
        {
            var s = v / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        return (0.2126 * Channel(c.R)) + (0.7152 * Channel(c.G)) + (0.0722 * Channel(c.B));
    }

    private static double Ratio(Color a, Color b)
    {
        var (hi, lo) = (Math.Max(Luminance(a), Luminance(b)), Math.Min(Luminance(a), Luminance(b)));
        return (hi + 0.05) / (lo + 0.05);
    }

    private static Color Token(string key) =>
        Application.Current is { } app && app.TryGetResource(key, app.ActualThemeVariant, out var found) && found is ISolidColorBrush brush ? brush.Color : throw new InvalidOperationException(key);

    // A fill with no colour of its own shows what is behind it: the window.
    private static Color Fill(IBrush? brush) => brush is ISolidColorBrush solid ? Over(solid.Color, Token("Bg.Window")) : Token("Bg.Window");

    private static void Set(StyledElement element, string pseudoClass, bool on) => ((IPseudoClasses)element.Classes).Set(pseudoClass, on);

    private static async Task Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(15);
        }
    }

    private static ContentPresenter Presenter(Control button) => button.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");

    private static async Task Docked(Func<StudioDock, DockControl, Task> body)
    {
        var dock = new StudioDock(editor: tab => new TextBlock { Text = tab.Key });
        var control = new DockControl();
        dock.Attach(control);
        var window = new Window { Width = 1000, Height = 700, Content = control };
        window.Show();
        try { await body(dock, control); }
        finally { window.Close(); }
    }

    [Fact]
    public Task TheTabCloseButtonShowsItsHoverFillInEveryStateOfTheTab() => HeadlessApp.Run(() => Docked(async (dock, control) =>
    {
        dock.OpenTab("resource", "sprites:spr_a", false);
        dock.OpenTab("resource", "sprites:spr_b", false);
        await Settle();
        var tabs = control.GetVisualDescendants().OfType<DocumentTabStripItem>().ToList();
        Assert.Equal(2, tabs.Count);
        var problems = new List<string>();
        (DocumentTabStripItem Tab, string State)[] cases = [(tabs[0], "plain"), (tabs[0], "hovered"), (tabs[1], "chosen"), (tabs[1], "chosen and hovered")];
        foreach (var (tab, state) in cases)
        {
            Set(tab, ":pointerover", state.EndsWith("hovered", StringComparison.Ordinal));
            var close = tab.GetVisualDescendants().OfType<Button>().First(b => b.Bounds.Width > 0);
            foreach (var pseudoClass in PointerStates)
            {
                Set(close, pseudoClass, true);
                await Settle();
                var tabFill = Fill(tab.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_TabBody").Background);
                var closeFill = Fill(Presenter(close).Background);
                var cross = tab.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().First(p => p.Name == "PART_ClosePath");
                var crossColour = ((ISolidColorBrush)cross.Fill!).Color;
                if (Ratio(tabFill, closeFill) < Strong) problems.Add($"{state} {pseudoClass}: close fill {closeFill} on tab {tabFill} is {Ratio(tabFill, closeFill):0.0}:1");
                if (Ratio(closeFill, crossColour) < 4.5) problems.Add($"{state} {pseudoClass}: cross {crossColour} on close fill {closeFill} is {Ratio(closeFill, crossColour):0.0}:1");
                Set(close, pseudoClass, false);
            }

            Set(tab, ":pointerover", false);
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }));

    [Fact]
    public Task ThePanelHeaderButtonsStandOutFromTheWindowUnderThePointer() => HeadlessApp.Run(() => Docked(async (dock, control) =>
    {
        dock.ShowPanel("Explorer");
        await Settle();
        var buttons = control.GetVisualDescendants().OfType<ToolChromeControl>().SelectMany(c => c.GetVisualDescendants().OfType<Button>()).Where(b => b.Name is "PART_MenuButton" or "PART_PinButton" or "PART_CloseButton").ToList();
        Assert.True(buttons.Count >= 2, "the pin and the menu button of a panel header");
        foreach (var button in buttons)
        {
            Set(button, ":pointerover", true);
            await Settle();
            Assert.True(Ratio(Fill(Presenter(button).Background), Token("Bg.Window")) >= Visible, $"{button.Name} hover fill is too close to the window");
            Set(button, ":pointerover", false);
        }
    }));

    [Fact]
    public Task AButtonInAListRowKeepsItsHoverAndPressedFillApartFromTheRowInEveryRowState() => HeadlessApp.Run(async () =>
    {
        var list = new ListBox { ItemsSource = new object[] { new Button { Content = "one" } }, SelectedIndex = 0 };
        var window = new Window { Width = 300, Height = 200, Content = list };
        window.Show();
        try
        {
            await Settle();
            var row = list.GetVisualDescendants().OfType<ListBoxItem>().First();
            var button = row.GetVisualDescendants().OfType<Button>().First();
            var problems = new List<string>();
            foreach (var rowState in RowStates)
            {
                foreach (var pseudoClass in RowFlags) Set(row, pseudoClass, rowState.Contains(pseudoClass, StringComparison.Ordinal));
                foreach (var buttonState in PointerStates)
                {
                    Set(button, buttonState, true);
                    await Settle();
                    var rowFill = Fill(Presenter(row).Background);
                    var buttonFill = Fill(Presenter(button).Background);
                    if (Ratio(rowFill, buttonFill) < Visible) problems.Add($"row '{rowState}' with button '{buttonState}': {buttonFill} on {rowFill} is {Ratio(rowFill, buttonFill):0.0}:1");
                    Set(button, buttonState, false);
                }
            }

            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        }
        finally { window.Close(); }
    });

    private static Color PixelAt(Window window, Control item, double dx)
    {
        var at = item.TranslatePoint(new Point(dx, item.Bounds.Height / 2), window)!.Value;
        using var frame = window.CaptureRenderedFrame()!;
        using var locked = frame.Lock();
        var (x, y) = ((int)Math.Round(at.X * frame.Dpi.X / 96), (int)Math.Round(at.Y * frame.Dpi.Y / 96));
        var bgra = System.Runtime.InteropServices.Marshal.ReadInt32(locked.Address, (y * locked.RowBytes) + (x * 4));
        return Color.FromRgb((byte)(bgra >> 16), (byte)(bgra >> 8), (byte)bgra);
    }

    // A chosen tab or row keeps its fill (and with it the dark label that sits on it) under a real pointer; the page is checked as drawn, pixel by pixel.
    [Fact]
    public Task AChosenTabOrRowKeepsItsFillUnderTheRealPointer() => HeadlessApp.Run(async () =>
    {
        var tabs = new TabControl { Items = { new TabItem { Header = "GLSL vertex", Content = "a" }, new TabItem { Header = "GLSL fragment", Content = "b" } }, SelectedIndex = 1 };
        var list = new ListBox { ItemsSource = new List<string> { "one", "two" }, SelectedIndex = 1 };
        var window = new Window { Width = 400, Height = 300, Content = new StackPanel { Children = { tabs, list } } };
        window.Show();
        try
        {
            await Settle();
            var problems = new List<string>();
            foreach (var item in new Control[] { tabs.GetVisualDescendants().OfType<TabItem>().Last(), list.GetVisualDescendants().OfType<ListBoxItem>().Last() })
            {
                window.MouseMove(new Point(390, 290)); // away from everything
                await Settle();
                var plain = PixelAt(window, item, 3);
                window.MouseMove(item.TranslatePoint(new Point(item.Bounds.Width / 2, item.Bounds.Height / 2), window)!.Value);
                await Settle();
                var hovered = PixelAt(window, item, 3);
                if (plain != hovered) problems.Add($"{item.GetType().Name}: fill {plain} turns {hovered} under the pointer");
            }

            Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task AHoveredTreeRowLeavesTheCaptionOfASelectedRowUnderItDark() => HeadlessApp.Run(async () =>
    {
        var caption = new TextBlock { Text = "child", Classes = { "muted" } };
        var child = new TreeViewItem { Header = caption };
        var parent = new TreeViewItem { Header = "parent", IsExpanded = true, Items = { child } };
        var window = new Window { Width = 400, Height = 300, Content = new TreeView { Items = { parent } } };
        window.Show();
        try
        {
            await Settle();
            Set(child, ":selected", true);
            Set(parent, ":pointerover", true); // the pointer over a row is over every row above it too
            await Settle();
            Assert.Equal(Token("Text.OnAccent"), (caption.Foreground as ISolidColorBrush)?.Color);
        }
        finally { window.Close(); }
    });
}
