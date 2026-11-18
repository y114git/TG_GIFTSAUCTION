using Avalonia.Controls;
using Avalonia.Threading;
using G3MStudio.Core.Notifications;
using G3MStudio.Notifications;
using G3MStudio.Theme;

namespace G3MStudio.Tests;

/// <summary>Opens every kind of popup with real timed animations: a property that has no animator crashes only in a real window, not in an instant headless run.</summary>
public sealed class PopupMotionSweepTests
{
    private static readonly string[] Items = ["a", "b"];

    [Theory]
    [InlineData("Medium")]
    [InlineData("Full")]
    public async Task EveryKindOfPopupAndCueAnimatesWithoutAnError(string level)
    {
        await HeadlessApp.Run(async () =>
        {
            Motion.TimedInHeadless = true;
            Motion.SetLevel(level);
            var window = new Window { Width = 600, Height = 400 };
            try
            {
                var toasts = new ToastHost();
                var center = new NotificationCenter();
                toasts.Bind(center);
                var combo = new ComboBox { ItemsSource = Items, SelectedIndex = 0 };
                var flyout = new Button { Flyout = new Flyout { Content = new TextBlock { Text = "x" } } };
                var menu = new Button { Flyout = new MenuFlyout { Items = { new MenuItem { Header = "x" } } } };
                var context = new Border { Width = 50, Height = 50, ContextMenu = new ContextMenu { Items = { new MenuItem { Header = "x" } } } };
                var tipped = new Button { Content = "tip" };
                ToolTip.SetTip(tipped, "tip");
                var cued = new Border[8].Select(_ => new Border { Width = 20, Height = 20 }).ToArray();
                var panel = new StackPanel();
                foreach (var control in new Control[] { toasts, combo, flyout, menu, context, tipped }.Concat(cued)) panel.Children.Add(control);
                window.Content = panel;
                window.Show();
                Dispatcher.UIThread.RunJobs();

                flyout.Flyout!.ShowAt(flyout);
                menu.Flyout!.ShowAt(menu);
                context.ContextMenu!.Open(context);
                combo.IsDropDownOpen = true;
                ToolTip.SetIsOpen(tipped, true);
                center.Post(NotificationLevel.Info, "hello");
                center.Post(NotificationLevel.Error, "failed");
                var dialog = new Window { Width = 300, Height = 200 };
                DialogFrame.Show(dialog, new TextBlock { Text = "d" }, [DialogFrame.Close(dialog)]);
                dialog.Show(window);

                Motion.Open(cued[0]);
                Motion.Shake(cued[1]);
                Motion.Flash(cued[2]);
                Motion.Enter(cued[3], scale: true, fromX: 24, fromY: 24);
                Motion.Exit(cued[4], () => { });
                Motion.Leave(cued[5], 24, 24, () => { });
                Motion.Fold(cued[6]);
                Motion.Slide(cued[7], 24, 0);

                for (var i = 0; i < 20; i++)
                {
                    await Task.Delay(30);
                    Dispatcher.UIThread.RunJobs();
                }

                Assert.Null(Motion.LastFailure);
                dialog.Close();
            }
            finally
            {
                window.Close();
                Motion.TimedInHeadless = false;
                Motion.SetLevel("Medium");
            }
        });
    }
}
