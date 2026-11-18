using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>Tooltips and hover cards wrap long text inside their box and never cut it (item 52).</summary>
public sealed class TooltipClipTests
{
    // 90 characters without a space, like the names of generated code entries.
    private static readonly string Long = "gml_Script_anon_gml_Object_obj_event_manager_Create_0_4484_gml_Object_obj_event_manager_".PadRight(90, 'x');

    private static async Task<(Window Window, ToolTip Tip)> Open(Control control, Window window)
    {
        window.Content = control;
        window.Show();
        Dispatcher.UIThread.RunJobs();
        ToolTip.SetIsOpen(control, true);
        for (var i = 0; i < 5; i++)
        {
            await Task.Delay(40);
            Dispatcher.UIThread.RunJobs();
        }

        return (window, window.GetVisualDescendants().OfType<ToolTip>().Single());
    }

    // Every text of the tip lies inside the tip's own content box, which in turn lies inside the tip.
    private static void AssertNothingCut(ToolTip tip)
    {
        var presenter = tip.GetVisualDescendants().OfType<Avalonia.Controls.Presenters.ContentPresenter>().First();
        var inner = presenter.Bounds.Width + 0.5;
        Assert.True(presenter.Bounds.Right <= tip.Bounds.Width + 0.5, $"content {presenter.Bounds} sticks out of the tip {tip.Bounds}");
        foreach (var text in tip.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && t.Bounds.Width > 0))
        {
            var left = text.TranslatePoint(default, presenter)!.Value.X;
            Assert.True(left + text.Bounds.Width <= inner, $"{text.Name ?? "text"} {text.Bounds} is wider than the tip content {presenter.Bounds}");
        }
    }

    [Fact]
    public async Task AHoverCardWrapsAnUnbrokenNinetyCharacterNameInsideItsBox()
    {
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            HoverCards.Describe = key => new ResourceHoverInfo(key, "scripts", Long, null, 675);
            var button = new Button { Content = "x", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            HoverCards.Attach(button, "scripts:" + Long, Long);
            var (window, tip) = await Open(button, new Window { Width = 700, Height = 300 });
            try
            {
                var card = tip.GetVisualDescendants().OfType<ResourceHoverCard>().Single();
                var title = card.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "HoverTitle");
                Assert.Equal(Long, title.Text);
                Assert.True(title.Bounds.Height >= title.FontSize * 2, $"the name did not wrap: {title.Bounds}");
                AssertNothingCut(tip);
                Assert.True(card.Bounds.Width <= ResourceHoverCard.MaxCard + 0.5);
            }
            finally
            {
                ToolTip.SetIsOpen(button, false);
                window.Close();
            }
        });
    }

    [Fact]
    public async Task ACardWithANicknameAndALongDetailWrapsEveryLine()
    {
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            HoverCards.Describe = key => new ResourceHoverInfo(key, "scripts", Long, "A nickname that is long enough to need two lines in the card of a resource, with " + Long, 675);
            var button = new Button { Content = "x", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            HoverCards.Attach(button, "scripts:" + Long, Long, "Used 12 times in this code, " + Long);
            var (window, tip) = await Open(button, new Window { Width = 700, Height = 300 });
            try
            {
                AssertNothingCut(tip);
                var card = tip.GetVisualDescendants().OfType<ResourceHoverCard>().Single();
                Assert.All(card.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible), t => Assert.Equal(Avalonia.Media.TextWrapping.Wrap, t.TextWrapping));
            }
            finally
            {
                ToolTip.SetIsOpen(button, false);
                window.Close();
            }
        });
    }

    [Fact]
    public async Task APlainTooltipWrapsALongNameToo()
    {
        await HeadlessApp.Run(async () =>
        {
            var button = new Button { Content = "x", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
            ToolTip.SetTip(button, Long);
            var (window, tip) = await Open(button, new Window { Width = 700, Height = 300 });
            try { AssertNothingCut(tip); }
            finally
            {
                ToolTip.SetIsOpen(button, false);
                window.Close();
            }
        });
    }
}
