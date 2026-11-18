using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

/// <summary>Popups live in their own top level, so the interface scale must reach them: they open as large as the window content around them.</summary>
public sealed class PopupScaleTests
{
    private static readonly string[] Items = ["one", "two"];

    /// <summary>How many times larger than its layout size a control shows on the screen of its own top level (the popup's, for a popup).</summary>
    private static double Shown(Visual visual)
    {
        if (visual.TransformToVisual(TopLevel.GetTopLevel(visual)!) is not { } matrix) throw new InvalidOperationException($"{visual.GetType().Name} parent={visual.GetVisualParent()?.GetType().Name} top={TopLevel.GetTopLevel(visual)?.GetType().Name}");
        return Math.Sqrt(matrix.M11 * matrix.M11 + matrix.M12 * matrix.M12);
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(30);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static MainWindow Scaled(TempDir dir, int scale)
    {
        var window = TabsWorkspaceTests.NewWindow(dir);
        window.Workspaces.Settings.Update(s => s with { UiScale = scale });
        return window;
    }

    [Theory]
    [InlineData(100)]
    [InlineData(150)]
    public async Task AMenuAComboBoxListATooltipAndTheOverflowDropDownOpenAtTheInterfaceScale(int scale)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = Scaled(dir, scale);
            try
            {
                var expected = scale / 100.0;
                Assert.Equal(expected, window.ZoomFactor);

                var file = window.MainMenu.Items.OfType<MenuItem>().First();
                window.UpdateLayout();
                var at = file.TranslatePoint(new Point(file.Bounds.Width / 2, file.Bounds.Height / 2), window)!.Value;
                Avalonia.Headless.HeadlessWindowExtensions.MouseMove(window, at);
                Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, at, Avalonia.Input.MouseButton.Left);
                Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, at, Avalonia.Input.MouseButton.Left);
                await Settle();
                Assert.True(file.IsSubMenuOpen && file.IsEffectivelyVisible && file.Parent is not null, $"open={file.IsSubMenuOpen} vis={file.IsEffectivelyVisible} parent={file.Parent?.GetType().Name} root={TopLevel.GetTopLevel(file)?.GetType().Name} n={window.MainMenu.Items.Count}");
                Assert.Equal(expected, Shown(file.Items.OfType<MenuItem>().First()), 2);
                file.IsSubMenuOpen = false;

                var combo = new ComboBox { ItemsSource = Items, SelectedIndex = 0 };
                var button = new Button { Content = "x" };
                ToolTip.SetTip(button, "A tip");
                var bar = new G3MStudio.Theme.OverflowBar();
                var panel = new StackPanel { Children = { combo, button, bar.More } };
                var host = new Window { Content = panel };
                host.Show();
                try
                {
                    // The host is a dialog: it is scaled by the dialog sizing, and what opens from it must be scaled once, not twice.
                    combo.IsDropDownOpen = true;
                    await Settle();
                    Assert.Equal(expected, Shown(combo.ContainerFromIndex(1)!), 2);
                    combo.IsDropDownOpen = false;

                    ToolTip.SetIsOpen(button, true);
                    await Settle();
                    Assert.Equal(expected, Shown(host.GetVisualDescendants().OfType<ToolTip>().Single()), 2);
                    ToolTip.SetIsOpen(button, false);

                    var dropDown = bar.More.Flyout!;
                    dropDown.ShowAt(bar.More);
                    await Settle();
                    Assert.Equal(expected, Shown(bar.Spill), 2);
                    dropDown.Hide();

                    // A popup placed against the window itself (the completion description) has no transform of a control to inherit.
                    var onWindow = new Popup { PlacementTarget = host, Child = new TextBlock { Text = "described" } };
                    panel.Children.Add(onWindow);
                    onWindow.IsOpen = true;
                    await Settle();
                    Assert.Equal(expected, Shown(onWindow.Child), 2);
                    onWindow.IsOpen = false;
                }
                finally { host.Close(); }
            }
            finally { window.Close(); }
        });
    }
}
