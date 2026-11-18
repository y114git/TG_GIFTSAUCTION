using Avalonia.Controls;
using Avalonia.Threading;
using G3MStudio.Docking;

#pragma warning disable CA2000 // windows made for a check only

namespace G3MStudio.Tests;

public sealed class MinSizeScrollerTests
{
    private static (Window Window, Border Panel, ScrollViewer Scroll) Show(double width, double height)
    {
        var panel = new Border();
        var host = new MinSizeScroller(panel, 200, 160);
        var window = new Window { Content = host, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return (window, panel, (ScrollViewer)host.Child!);
    }

    [Fact]
    public async Task ASmallSpaceKeepsThePanelAtItsMinimumAndScrollsIt()
    {
        await HeadlessApp.Run(() =>
        {
            var (window, panel, scroll) = Show(120, 90);
            try
            {
                Assert.True(panel.Bounds.Width >= 200 && panel.Bounds.Height >= 160, $"{panel.Bounds}");
                Assert.True(scroll.Extent.Height >= 160 && scroll.Viewport.Height < scroll.Extent.Height);
                Assert.True(scroll.Extent.Width >= 200 && scroll.Viewport.Width < scroll.Extent.Width);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ALargeSpaceIsFilledWithoutAScrollBar()
    {
        await HeadlessApp.Run(() =>
        {
            var (window, panel, scroll) = Show(500, 400);
            try
            {
                Assert.Equal(scroll.Viewport.Width, panel.Bounds.Width, 1);
                Assert.Equal(scroll.Viewport.Height, panel.Bounds.Height, 1);
                Assert.Equal(scroll.Extent.Height, scroll.Viewport.Height, 1);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task AnUnboundedParentDoesNotKeepTheLayoutBusy()
    {
        await HeadlessApp.Run(() =>
        {
            var panel = new CountingBorder();
            var host = new MinSizeScroller(panel, 200, 160);
            // A ScrollViewer measures its content without a height limit but arranges it as tall as itself.
            var window = new Window { Content = new ScrollViewer { Content = host }, Width = 500, Height = 400 };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var settled = panel.Measures;
                for (var i = 0; i < 5; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                Assert.Equal(settled, panel.Measures);
                Assert.True(panel.Bounds.Height >= 160, $"{panel.Bounds}");
            }
            finally { window.Close(); }
        });
    }

    // A host that arranges the wrapper wider than it measured it, as a real parent can.
    private sealed class WideArrange(double extra) : Decorator
    {
        protected override Avalonia.Size ArrangeOverride(Avalonia.Size finalSize)
        {
            base.ArrangeOverride(new Avalonia.Size(finalSize.Width + extra, finalSize.Height));
            return finalSize;
        }
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(10, true)]
    public async Task AnArrangeOnePixelOffItsMeasureIsRoundingAndOneTenPixelsOffMeasuresThePanelAgain(double extra, bool measuresAgain)
    {
        await HeadlessApp.Run(() =>
        {
            var panel = new CountingBorder();
            var wrapper = new WideArrange(extra) { Child = new MinSizeScroller(panel, 200, 160) };
            var window = new Window { Content = wrapper, Width = 500, Height = 400 };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var first = panel.Measures;
                for (var i = 0; i < 5; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                }

                Assert.Equal(first, panel.Measures); // it settles
                var plain = new CountingBorder();
                var reference = new Window { Content = new MinSizeScroller(plain, 200, 160), Width = 500, Height = 400 };
                reference.Show();
                Dispatcher.UIThread.RunJobs();
                reference.UpdateLayout();
                try { Assert.Equal(measuresAgain, first > plain.Measures); }
                finally { reference.Close(); }
            }
            finally { window.Close(); }
        });
    }

    private sealed class CountingBorder : Border
    {
        public int Measures { get; private set; }

        protected override Avalonia.Size MeasureOverride(Avalonia.Size availableSize)
        {
            Measures++;
            return base.MeasureOverride(availableSize);
        }
    }
}
