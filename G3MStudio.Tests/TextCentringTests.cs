using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

/// <summary>Rules R1 and R2: the text of a button, toggle, tab, combo box and list row sits in the middle (equal gaps above and below, and left and right on a button) and keeps 8 px from the sides.</summary>
public sealed class TextCentringTests
{
    private static readonly string[] Choices = ["Choice"], Rows = ["Row one", "Row two"];

    private static (double Left, double Top, double Right, double Bottom) Gaps(Control control, string text)
    {
        var block = control.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == text);
        var box = new Rect(default, block.Bounds.Size).TransformToAABB(block.TransformToVisual(control)!.Value);
        return (box.Left, box.Top, control.Bounds.Width - box.Right, control.Bounds.Height - box.Bottom);
    }

    [Fact]
    public Task TextIsCentredAndKeepsEightPixelsFromTheSides() => HeadlessApp.Run(() =>
    {
        var button = new Button { Content = "Button", Width = 160, Height = 44 };
        var primary = new Button { Content = "Primary", Classes = { "primary" }, Width = 160, Height = 44 };
        var toggle = new ToggleButton { Content = "Toggle", Width = 160, Height = 44 };
        var combo = new ComboBox { ItemsSource = Choices, SelectedIndex = 0, Width = 160, Height = 44 };
        var tabs = new TabControl { Width = 300, Height = 120, ItemsSource = new TabItem[] { new() { Header = "Tab one", Content = "c" }, new() { Header = "Tab two" } } };
        var list = new ListBox { Width = 160, ItemsSource = Rows };
        var window = new Window { Width = 500, Height = 500, Content = new StackPanel { Spacing = 4, Children = { button, primary, toggle, combo, tabs, list } } };
        window.Show();
        try
        {
            window.UpdateLayout();
            foreach (var (control, text) in (ReadOnlySpan<(Control, string)>)[(button, "Button"), (primary, "Primary"), (toggle, "Toggle")])
            {
                var (left, top, right, bottom) = Gaps(control, text);
                Assert.True(Math.Abs(top - bottom) <= 1, $"{text}: gap above {top}, below {bottom}");
                Assert.True(Math.Abs(left - right) <= 1, $"{text}: gap left {left}, right {right}");
                Assert.True(left >= 8, $"{text}: {left} px from the left edge");
            }

            var (comboLeft, comboTop, _, comboBottom) = Gaps(combo, "Choice");
            Assert.True(Math.Abs(comboTop - comboBottom) <= 1, $"combo box: gap above {comboTop}, below {comboBottom}");
            Assert.True(comboLeft >= 8, $"combo box: {comboLeft} px from the left edge");

            var tab = tabs.GetVisualDescendants().OfType<TabItem>().First();
            var (tabLeft, tabTop, tabRight, tabBottom) = Gaps(tab, "Tab one");
            Assert.True(Math.Abs(tabTop - tabBottom) <= 1, $"tab: gap above {tabTop}, below {tabBottom}");
            Assert.True(Math.Abs(tabLeft - tabRight) <= 1 && tabLeft >= 8, $"tab: gap left {tabLeft}, right {tabRight}");

            var row = list.GetVisualDescendants().OfType<ListBoxItem>().First();
            var (rowLeft, rowTop, _, rowBottom) = Gaps(row, "Row one");
            Assert.True(Math.Abs(rowTop - rowBottom) <= 1, $"list row: gap above {rowTop}, below {rowBottom}");
            Assert.True(rowLeft >= 8, $"list row: {rowLeft} px from the left edge");
        }
        finally { window.Close(); }
        return Task.CompletedTask;
    });
}
