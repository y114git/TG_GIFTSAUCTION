using Avalonia;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using G3MLib.DataFile.Models;
using G3MStudio.Editor;

namespace G3MStudio.Tests;

public sealed class CurveViewTests
{
    private static GameMakerAnimationCurve.Channel Channel(string name, float from, float to)
    {
        var channel = new GameMakerAnimationCurve.Channel { Name = new GameMakerString(name), Points = [] };
        channel.Points.Add(new GameMakerAnimationCurve.Channel.Point { X = 0, Value = from });
        channel.Points.Add(new GameMakerAnimationCurve.Channel.Point { X = 1, Value = to });
        return channel;
    }

    [Fact]
    public async Task ACurveWithTwoChannelsDrawsSomethingOtherThanTheBackground()
    {
        await HeadlessApp.Run(() =>
        {
            var curve = new GameMakerAnimationCurve { Channels = [] };
            curve.Channels.Add(Channel("a", 0, 1));
            curve.Channels.Add(Channel("b", 1, 0));
            var view = new AnimationCurveView(curve) { Width = 200 };
            view.Measure(new Size(200, 260));
            view.Arrange(new Rect(0, 0, 200, 260));
            using var target = new RenderTargetBitmap(new PixelSize(200, 260));
            target.Render(view);
            using var empty = new RenderTargetBitmap(new PixelSize(200, 260));
            using var plain = new MemoryStream();
            using var drawn = new MemoryStream();
            target.Save(drawn, new PngBitmapEncoderOptions());
            empty.Save(plain, new PngBitmapEncoderOptions());
            Assert.NotEqual(plain.ToArray(), drawn.ToArray());
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ATimelineWithANonFiniteLengthStillDrawsAndTicksStayFarApart()
    {
        await HeadlessApp.Run(() =>
        {
            var sequence = new GameMakerSequence { Name = new GameMakerString("seq_bad"), Length = float.PositiveInfinity, Tracks = [] };
            var view = new SequenceTimelineView(sequence) { Width = 600 };
            Assert.Equal(1, view.Length);
            view.Measure(new Size(600, 200));
            view.Arrange(new Rect(0, 0, 600, 200));
            using var target = new RenderTargetBitmap(new PixelSize(600, 200));
            target.Render(view);
            Assert.Equal(1, SequenceTimelineView.Step(100));
            Assert.True(SequenceTimelineView.Step(0.001) * 0.001 >= 48);
            Assert.True(SequenceTimelineView.Step(1e-30) > 1e9);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task PickingARowOfThePointListSelectsThePointOnTheCanvas()
    {
        await HeadlessApp.Run(() =>
        {
            var path = new GameMakerPath { Name = new GameMakerString("p"), Points = [] };
            for (var i = 0; i < 3; i++) path.Points.Add(new GameMakerPath.PathPoint { X = i * 10, Y = i * 5 });
            var editor = new PathEditor(path, null);
            var list = editor.GetVisualDescendants().OfType<ListBox>().Single(l => l.Name == "PathPoints");
            Assert.Equal(3, list.ItemCount);
            list.SelectedIndex = 2;
            Assert.Equal(2, editor.View.Selected);
            editor.View.Selected = 1;
            Assert.Equal(1, list.SelectedIndex);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task ThePathCanvasFitsAllPointsInsideItself()
    {
        await HeadlessApp.Run(() =>
        {
            var path = new GameMakerPath { Name = new GameMakerString("p"), Points = [] };
            foreach (var (x, y) in new[] { (0f, 0f), (100f, -75f), (200f, 0f) }) path.Points.Add(new GameMakerPath.PathPoint { X = x, Y = y });
            var view = new PathView(path, null);
            view.Measure(new Size(400, 320));
            view.Arrange(new Rect(0, 0, 400, 320));
            foreach (var point in path.Points)
            {
                var at = view.ToScreen(new Point(point.X, point.Y));
                Assert.InRange(at.X, 0, 400);
                Assert.InRange(at.Y, 0, 320);
            }

            Assert.True(view.ToScreen(new Point(200, 0)).X - view.ToScreen(new Point(0, 0)).X > 200);
            return Task.CompletedTask;
        });
    }
}
