using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>The pinned heading is checked on rendered pixels: while the list scrolls under it, no pixel of a resource row may show in the strip at the top of the list.</summary>
public sealed class ExplorerStickyPixelTests
{
    private static T Named<T>(Visual view, string name) where T : Control => view.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    private static byte[] Render(Window window, double device, out int stride)
    {
        var size = new PixelSize((int)Math.Ceiling(window.Bounds.Width * device), (int)Math.Ceiling(window.Bounds.Height * device));
        using var target = new RenderTargetBitmap(size, new Vector(96 * device, 96 * device));
        target.Render(window);
        stride = size.Width * 4;
        var pixels = new byte[stride * size.Height];
        var pin = System.Runtime.InteropServices.GCHandle.Alloc(pixels, System.Runtime.InteropServices.GCHandleType.Pinned);
        try { target.CopyPixels(new PixelRect(size), pin.AddrOfPinnedObject(), pixels.Length, stride); }
        finally { pin.Free(); }
        return pixels;
    }

    // Interface scale (the zoom host of the main window) times the scale of the screen gives the fractional device pixels where strips used to show.
    [Theory]
    [InlineData(1.0, 1.0, false)]
    [InlineData(1.0, 1.0, true)]
    [InlineData(1.25, 1.0, false)]
    [InlineData(1.25, 1.0, true)]
    [InlineData(1.5, 1.0, false)]
    [InlineData(1.5, 1.0, true)]
    [InlineData(2.0, 1.0, false)]
    [InlineData(1.0, 1.25, false)]
    [InlineData(1.25, 1.5, true)]
    public async Task NoResourceRowShowsAboveOrUnderThePinnedHeadingWhileTheListScrolls(double scale, double device, bool icons)
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var view = new ExplorerView();
            var host = new LayoutTransformControl { LayoutTransform = new ScaleTransform(scale, scale), Child = view };
            var window = new Window { Width = 320 * scale, Height = 400 * scale, Content = host };
            window.Show();
            Avalonia.Headless.HeadlessWindowExtensions.SetRenderScaling(window, device); // layout rounding then works in the device pixels of that screen
            try
            {
                using var data = ExplorerModelTests.Generate(40);
                view.Load("game.win", data);
                // Quick access above the data: recent rows under their own heading, then the data group and its types.
                view.Model.Quick = view.Model.Quick with { Recent = [.. Enumerable.Range(0, 6).Select(i => ResourceTypes.KeyOf("sprites", $"spr_{i}"))] };
                Named<ToggleButton>(view, "ThumbToggle").IsChecked = icons;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var list = Named<ListBox>(view, "List");
                var sticky = Named<Border>(view, "Sticky");
                var scroll = list.GetVisualDescendants().OfType<ScrollViewer>().First();
                var rowHeight = list.GetRealizedContainers().Min(c => c.Bounds.Height);
                var listTop = list.TranslatePoint(default, window)!.Value.Y;
                var device0 = (int)Math.Floor(listTop * device);
                var device1 = (int)Math.Floor((listTop + rowHeight * scale) * device); // the pixel row shared with the row below the heading is left out
                var checkedItems = 0;
                for (var offset = 0.0; offset < scroll.Extent.Height - scroll.Viewport.Height; offset += rowHeight * 0.29 + 0.137)
                {
                    scroll.Offset = new Vector(0, offset);
                    Dispatcher.UIThread.RunJobs();
                    window.UpdateLayout();
                    Dispatcher.UIThread.RunJobs();
                    var items = list.GetRealizedContainers().Where(c => c.DataContext is ExplorerRow { IsItem: true }).ToList();
                    if (!items.Any(c => c.Bounds.Y - scroll.Offset.Y < rowHeight && c.Bounds.Bottom - scroll.Offset.Y > 0)) continue; // no row of a resource reaches the strip
                    checkedItems++;
                    var shown = Render(window, device, out var stride);
                    foreach (var c in items) c.Opacity = 0;
                    var bare = Render(window, device, out _);
                    foreach (var c in items) c.Opacity = 1;
                    for (var y = device0; y < device1; y++)
                    {
                        var at = y * stride;
                        for (var x = 0; x < stride; x++)
                        {
                            if (shown[at + x] == bare[at + x]) continue;
                            var top = string.Join("; ", from c in list.GetRealizedContainers() orderby c.Bounds.Y where c.Bounds.Bottom - scroll.Offset.Y > 0 select $"{(c.DataContext as ExplorerRow)?.Kind} '{(c.DataContext as ExplorerRow)?.Text}' {c.Bounds.Y - scroll.Offset.Y:0.##}+{c.Bounds.Height:0.##}");
                            Assert.Fail($"offset {offset:0.###}: a resource row shows at pixel ({x / 4}, {y}) of the strip {device0}..{device1}; pinned '{view.StickyRow?.Text}' visible {sticky.IsVisible} at {sticky.Bounds}; rows {top[..Math.Min(top.Length, 400)]}");
                        }
                    }
                }

                Assert.True(checkedItems > 20, $"only {checkedItems} offsets put a resource row under the strip");
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }
}
