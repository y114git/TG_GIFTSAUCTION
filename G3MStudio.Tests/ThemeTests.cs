using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

public sealed class ThemeTests
{
    // Add every control type the shell starts using, so its Fluent template is checked too.
    private static ScrollViewer BuildSamples()
    {
        var combo = new ComboBox { ItemsSource = new[] { "one", "two" }, SelectedIndex = 0 };
        var menuItem = new MenuItem { Header = "File", ItemsSource = new[] { new MenuItem { Header = "Open" }, new MenuItem { Header = "-" }, new MenuItem { Header = "Exit" } } };
        var panel = new StackPanel
        {
            Spacing = 4,
            Tag = new object[] { combo, menuItem }
        };
        foreach (var child in new Control[]
        {
            new Button { Content = "Button" },
            new ToggleButton { Content = "Toggle" },
            new CheckBox { Content = "Check", IsChecked = true },
            new RadioButton { Content = "Radio", IsChecked = true },
            new TextBox { Text = "Text" },
            combo,
            new ListBox { ItemsSource = new[] { "a", "b" }, SelectedIndex = 0 },
            new TreeView { ItemsSource = new[] { new TreeViewItem { Header = "node", Items = { new TreeViewItem { Header = "leaf" } }, IsExpanded = true } } },
            new TabControl { ItemsSource = new[] { new TabItem { Header = "t1", Content = "c1" }, new TabItem { Header = "t2" } } },
            new Menu { Items = { menuItem } },
            new Expander { Header = "Expander", Content = "body", IsExpanded = true },
            new ProgressBar { Value = 40 },
            new Slider { Value = 40 },
            new ScrollViewer { Content = new Border { Width = 600, Height = 600 }, Width = 100, Height = 100, HorizontalScrollBarVisibility = ScrollBarVisibility.Visible, VerticalScrollBarVisibility = ScrollBarVisibility.Visible },
            new ToolTip { Content = "Tip" },
            new ContextMenu { ItemsSource = new[] { new MenuItem { Header = "Cut" } } },
            new NumericUpDown { Value = 1 },
            new ToggleSwitch(),
            new GridSplitter { Height = 6 },
            new Separator(),
        })
        {
            panel.Children.Add(child);
        }
        return new ScrollViewer { Content = panel };
    }

    [Fact]
    public async Task EveryBorderInUsedTemplatesIsTwoPixels()
    {
        await HeadlessApp.Run(() =>
        {
            var samples = BuildSamples();
            var window = new Window { Width = 800, Height = 1200, Content = samples };
            window.Show();
            var panel = (StackPanel)samples.Content!;
            var parts = (object[])panel.Tag!;
            ((ComboBox)parts[0]).IsDropDownOpen = true;
            ((MenuItem)parts[1]).IsSubMenuOpen = true;
            window.UpdateLayout();

            var bad = new List<string>();
            var twoPixelSides = 0;
            foreach (var visual in window.GetVisualDescendants())
            {
                var thickness = visual switch
                {
                    Border b => b.BorderThickness,
                    TemplatedControl t => t.BorderThickness,
                    ContentPresenter p => p.BorderThickness,
                    _ => default(Thickness?),
                };
                if (thickness is not { } th) continue;
                foreach (var side in new[] { th.Left, th.Top, th.Right, th.Bottom })
                {
                    if (side == 2) twoPixelSides++;
                    else if (side != 0) bad.Add($"{string.Join(" < ", visual.GetVisualAncestors().OfType<TemplatedControl>().Take(2).Prepend(visual).Select(v => v.GetType().Name))} {th}");
                }
            }

            Assert.True(twoPixelSides > 0, "no 2 px border found, the walk is empty");
            Assert.Empty(bad.Distinct());
            window.Close();
        });
    }

    [Fact]
    public async Task WindowUsesTokenColors()
    {
        await HeadlessApp.Run(() =>
        {
            var window = new MainWindow();
            window.Show();
            Assert.Equal(Avalonia.Media.Color.Parse("#282828"), ((Avalonia.Media.ISolidColorBrush)window.Background!).Color);
            Assert.Equal(Avalonia.Media.Color.Parse("#E8E9EB"), ((Avalonia.Media.ISolidColorBrush)window.Foreground!).Color);
            window.Close();
        });
    }

    [Fact]
    public Task DefaultFontIsArialBoldWithAnOsFallback() => HeadlessApp.Run(() =>
    {
        var app = Application.Current!;
        Assert.True(app.TryGetResource("Font.Body", app.ActualThemeVariant, out var family));
        var names = Assert.IsType<Avalonia.Media.FontFamily>(family).FamilyNames;
        Assert.Equal("Arial", names[0]);
        Assert.True(names.Count > 1, "no fallback after Arial");
        Assert.True(app.TryGetResource("Font.Weight", app.ActualThemeVariant, out var weight));
        Assert.Equal(Avalonia.Media.FontWeight.Bold, weight);

        var window = new MainWindow();
        window.Show();
        Assert.Equal("Arial", window.FontFamily.FamilyNames[0]);
        Assert.Equal(Avalonia.Media.FontWeight.Bold, window.FontWeight);
        window.Close();
    });
}
