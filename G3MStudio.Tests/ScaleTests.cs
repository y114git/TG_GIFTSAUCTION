using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

/// <summary>Q39: borders are whole device pixels at every UI scale, checked on the rendered bitmap.</summary>
public sealed class ScaleTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public async Task MainWindowBordersAreWholeDevicePixelsAtEveryScale(double scale)
    {
        await HeadlessApp.Run(() =>
        {
            var window = new MainWindow();
            window.Show();
            window.SetRenderScaling(scale);
            window.UpdateLayout();
            var frame = window.CaptureRenderedFrame()!;
            Assert.Equal((int)Math.Round(window.Bounds.Width * scale), frame.PixelSize.Width);

            // 2 logical px, snapped by layout rounding to a whole number of device pixels.
            var allowed = new[] { (int)Math.Floor(2 * scale), (int)Math.Ceiling(2 * scale) };
            var checkedSides = 0;
            using (var pixels = frame.Lock())
            {
                foreach (var visual in window.GetVisualDescendants())
                {
                    var (thickness, brush) = visual switch
                    {
                        Border b => (b.BorderThickness, b.BorderBrush),
                        TemplatedControl t => (t.BorderThickness, t.BorderBrush),
                        ContentPresenter p => (p.BorderThickness, p.BorderBrush),
                        _ => (default, null),
                    };
                    if (brush is not ISolidColorBrush { Color.A: 255 } solid || visual is not Visual v || v.Bounds.Width < 8 || v.Bounds.Height < 8) continue;
                    var origin = v.TranslatePoint(default, window)!.Value;
                    var left = (int)Math.Round(origin.X * scale);
                    var top = (int)Math.Round(origin.Y * scale);
                    var width = (int)Math.Round(v.Bounds.Width * scale);
                    var height = (int)Math.Round(v.Bounds.Height * scale);
                    foreach (var (side, dx, dy, x, y) in new[]
                    {
                        (thickness.Left, 1, 0, left, top + height / 2),
                        (thickness.Right, -1, 0, left + width - 1, top + height / 2),
                        (thickness.Top, 0, 1, left + width / 2, top),
                        (thickness.Bottom, 0, -1, left + width / 2, top + height - 1),
                    })
                    {
                        if (side == 0) continue;
                        var run = 0;
                        while (SameColor(pixels, x + dx * run, y + dy * run, solid.Color)) run++;
                        Assert.True(allowed.Contains(run), $"{v.GetType().Name} side {side}: {run} device px at {scale:P0}, expected {string.Join(" or ", allowed)}");
                        checkedSides++;
                    }
                }
            }

            Assert.True(checkedSides > 0, "no border side was found in the rendered window");
            window.Close();
        });
    }

    private static bool SameColor(ILockedFramebuffer pixels, int x, int y, Color color)
    {
        if (x < 0 || y < 0 || x >= pixels.Size.Width || y >= pixels.Size.Height) return false;
        var bytes = new byte[4];
        System.Runtime.InteropServices.Marshal.Copy(pixels.Address + y * pixels.RowBytes + x * 4, bytes, 0, 4);
        var (r, b) = pixels.Format == PixelFormat.Bgra8888 ? (bytes[2], bytes[0]) : (bytes[0], bytes[2]);
        return r == color.R && bytes[1] == color.G && b == color.B;
    }
}
