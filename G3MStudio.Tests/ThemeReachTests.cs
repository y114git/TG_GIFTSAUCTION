using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

public sealed class ThemeReachTests
{
    private static readonly string[] Items = ["a", "b"];

    private static string Colors(Window window)
    {
        var item = window.GetVisualDescendants().OfType<ListBoxItem>().First(i => i.IsSelected);
        var presenter = item.GetVisualDescendants().OfType<ContentPresenter>().First(p => p.Name == "PART_ContentPresenter");
        var check = window.GetVisualDescendants().OfType<CheckBox>().First();
        var box = check.GetVisualDescendants().OfType<Border>().First(b => b.Name == "NormalRectangle");
        var button = window.GetVisualDescendants().OfType<Button>().First();
        var text = window.GetVisualDescendants().OfType<TextBox>().First();
        var textBorder = text.GetVisualDescendants().OfType<Border>().First(b => b.Name == "PART_BorderElement");
        return $"selectedRow={(presenter.Background as ISolidColorBrush)?.Color} checkFill={(box.Background as ISolidColorBrush)?.Color} buttonBorder={button.BorderThickness} buttonBg={(button.GetVisualDescendants().OfType<ContentPresenter>().First().Background as ISolidColorBrush)?.Color} textBorder={textBorder.BorderThickness} textBg={(textBorder.Background as ISolidColorBrush)?.Color} textFg={(text.Foreground as ISolidColorBrush)?.Color}";
    }

    private static Avalonia.Thickness Padding(Window window) => window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "Probe").Padding;

    [Fact]
    public async Task AColorOrSizeChangedInSettingsReachesTheControlsAndAResetTakesItBack()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var list = new ListBox { ItemsSource = Items, SelectedIndex = 0 };
                var panel = new StackPanel { Children = { list, new CheckBox { IsChecked = true, Content = "c" }, new Button { Name = "Probe", Content = "b" }, new TextBox { Text = "t" } } };
                var host = new Window { Content = panel, Width = 400, Height = 400 };
                host.Show();
                await Task.Delay(200);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var before = Colors(host);
                var paddingBefore = Padding(host);
                window.Workspaces.Settings.Update(x => x with { Colors = new Dictionary<string, string> { ["Accent"] = "#FF0000", ["Bg.Sunken"] = "#102030", ["Text.Main"] = "#00FF00", ["Bg.Raised"] = "#304050" }, Sizes = new Dictionary<string, int> { ["Border.Width"] = 4, ["Space.Scale"] = 200 } });
                await Task.Delay(200);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                host.UpdateLayout();
                var after = Colors(host);
                // The accent reaches selected rows and check boxes, the border width and the surface and text colors reach a text box.
                Assert.Equal(paddingBefore.Left * 2, Padding(host).Left);
                Assert.Contains("selectedRow=Red checkFill=Red", after, StringComparison.Ordinal);
                Assert.Contains("textBorder=4,4,4,4 textBg=#ff102030 textFg=Lime", after, StringComparison.Ordinal);
                // A color chosen for the selected row wins over the accent.
                window.Workspaces.Settings.Update(x => x with { Colors = new Dictionary<string, string> { ["Accent"] = "#FF0000", ["Selection.Bg"] = "#112233" } });
                await Task.Delay(200);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                host.UpdateLayout();
                Assert.Contains("selectedRow=#ff112233 checkFill=Red", Colors(host), StringComparison.Ordinal);
                window.Workspaces.Settings.Update(x => x with { Colors = new Dictionary<string, string>(), Sizes = new Dictionary<string, int>() });
                await Task.Delay(200);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                host.UpdateLayout();
                Assert.Equal(before, Colors(host));
                Assert.Equal(paddingBefore, Padding(host));
                host.Close();
            }
            finally
            {
                window.Close();
            }
        });
    }
}
