using System.Text;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>Pixel-exact checks of the image editor tools on small bitmaps: '.' is clear, '#' red, 'b' blue, 'g' green.</summary>
public sealed class ImageToolTests
{
    private static readonly Rgba Red = new(255, 0, 0, 255), Blue = new(0, 0, 255, 255), Green = new(0, 255, 0, 255);

    internal static string Dump(PixelBuffer picture)
    {
        var text = new StringBuilder();
        for (var y = 0; y < picture.Height; y++)
        {
            for (var x = 0; x < picture.Width; x++) text.Append(picture.Get(x, y) == Red ? '#' : picture.Get(x, y) == Blue ? 'b' : picture.Get(x, y) == Green ? 'g' : picture.Get(x, y).A == 0 ? '.' : '?');
            if (y < picture.Height - 1) text.Append('/');
        }

        return text.ToString();
    }

    internal static PixelBuffer From(string art)
    {
        var rows = art.Split('/');
        var picture = new PixelBuffer(rows[0].Length, rows.Length);
        for (var y = 0; y < rows.Length; y++)
            for (var x = 0; x < rows[y].Length; x++) picture.Set(x, y, rows[y][x] switch { '#' => Red, 'b' => Blue, 'g' => Green, _ => Rgba.Clear });
        return picture;
    }

    private static string Run(string art, PixelSelection? selection, params ImageOp[] ops)
    {
        var state = new ImageState(From(art), selection);
        foreach (var op in ops) ImageOps.Run(state, op);
        return Dump(state.Picture);
    }

    private static int Count(PixelSelection selection) => selection.Mask.Count(m => m != 0);

    [Fact]
    public void BrushesStampSquareAndRoundShapes()
    {
        var round = new PixelBuffer(5, 5);
        round.Stamp(2, 2, 3, Red, BrushShape.Round);
        Assert.Equal("...../..#../.###./..#../.....", Dump(round));
        var square = new PixelBuffer(4, 4);
        square.Stamp(1, 1, 2, Red);
        Assert.Equal("..../.##./.##./....", Dump(square));
    }

    [Fact]
    public void StrokesJoinPointsAndTheEraserClears()
    {
        Assert.Equal("#.../.#../..#./...#", Run("..../..../..../....", null, new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "0,0 3,3")));
        Assert.Equal("#.../..../..#./...#", Run("..../..../..../....", null, new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "0,0 3,3"), new ImageOp(ImageOps.Stroke, "1", "0", "#00000000", "1,1")));
    }

    [Fact]
    public void RectanglesAreOutlinedFilledOrBoth()
    {
        Assert.Equal("#####/#bbb#/#bbb#/#####", Run("...../...../...../.....", null, new ImageOp(ImageOps.Rect, "#FF0000", "#0000FF", "1", "0,0 4,3")));
        Assert.Equal("######/######/##..##/##..##/######/######", Run("....../....../....../....../....../......", null, new ImageOp(ImageOps.Rect, "#FF0000", "-", "2", "0,0 5,5")));
        Assert.Equal("...../.bbb./.bbb./.....", Run("...../...../...../.....", null, new ImageOp(ImageOps.Rect, "-", "#0000FF", "1", "1,1 3,2")));
    }

    [Fact]
    public void EllipsesFollowTheBoxAndKeepTheOutlineAndFillApart()
    {
        const string Blank = "...../...../...../...../.....";
        Assert.Equal(".bbb./bbbbb/bbbbb/bbbbb/.bbb.", Run(Blank, null, new ImageOp(ImageOps.Ellipse, "-", "#0000FF", "1", "0,0 4,4")));
        Assert.Equal(".###./#bbb#/#bbb#/#bbb#/.###.", Run(Blank, null, new ImageOp(ImageOps.Ellipse, "#FF0000", "#0000FF", "1", "0,0 4,4")));
        Assert.Equal(".###./#...#/#...#/#...#/.###.", Run(Blank, null, new ImageOp(ImageOps.Ellipse, "#FF0000", "-", "1", "0,0 4,4")));
    }

    [Fact]
    public void APolygonFillsItsInsideAndOutlinesItsEdges()
    {
        const string Blank = "....../....../....../....../....../......";
        Assert.Equal("....../.####./.#bb#./.#bb#./.####./......", Run(Blank, null, new ImageOp(ImageOps.Polygon, "#FF0000", "#0000FF", "1", "0", "1,1 4,1 4,4 1,4")));
        Assert.Equal("....../.bbbb./.bbbb./.bbbb./.bbbb./......", Run(Blank, null, new ImageOp(ImageOps.Polygon, "#0000FF", "#0000FF", "1", "0", "1,1 4,1 4,4 1,4")));
    }

    [Fact]
    public void ACurveEndsWhereTheControlPointsEnd()
    {
        var picture = new PixelBuffer(9, 9);
        picture.Curve([(0, 8), (4, 0), (8, 8)], 1, Red, BrushShape.Square);
        Assert.Equal(Red, picture.Get(0, 8));
        Assert.Equal(Red, picture.Get(8, 8));
        Assert.Equal(Red, picture.Get(4, 4)); // the middle of a quadratic curve is half way to the control point
        Assert.Equal(Rgba.Clear, picture.Get(4, 0));
    }

    [Fact]
    public void FillUsesToleranceAndTheContiguousOption()
    {
        var grey = new PixelBuffer(4, 1);
        foreach (var (x, v) in new[] { (0, 100), (1, 100), (2, 110), (3, 200) }) grey.Set(x, 0, new Rgba((byte)v, (byte)v, (byte)v, 255));
        var exact = grey.Copy();
        exact.Fill(0, 0, Red);
        Assert.Equal("##??", Dump(exact));
        var loose = grey.Copy();
        loose.Fill(0, 0, Red, 10);
        Assert.Equal("###?", Dump(loose));
        Assert.Equal("#g#g", Run("#.#.", null, new ImageOp(ImageOps.Fill, "#00FF00", "1,0", "0", "0")));
        Assert.Equal("#g#.", Run("#.#.", null, new ImageOp(ImageOps.Fill, "#00FF00", "1,0", "0", "1")));
    }

    [Fact]
    public void ReplaceAndRemoveColourWorkOnEveryPixelOfTheClickedColour()
    {
        Assert.Equal("b.b/.b.", Run("#.#/.#.", null, new ImageOp(ImageOps.Replace, "0,0", "#0000FF", "0")));
        Assert.Equal("..b/b..", Run("#.b/b.#", null, new ImageOp(ImageOps.Remove, "0,0", "0")));
    }

    [Fact]
    public void SelectionsFromRectangleLassoWandAndTheirOperations()
    {
        var wand = From("#../.#./..#");
        Assert.Equal(3, Count(PixelSelection.Wand(wand, 1, 0, 0, true)));
        Assert.Equal(6, Count(PixelSelection.Wand(wand, 1, 0, 0, false)));
        var dot = PixelSelection.Rectangle(5, 5, 2, 2, 2, 2);
        Assert.Equal(5, Count(dot.Expanded(1)));
        Assert.Equal(1, Count(PixelSelection.Rectangle(5, 5, 1, 1, 3, 3).Expanded(-1)));
        Assert.Equal(8, Count(PixelSelection.Rectangle(5, 5, 1, 1, 3, 3).Border()));
        Assert.Equal(24, Count(dot.Inverted()));
        Assert.Equal(9, Count(PixelSelection.Lasso(5, 5, [(1, 1), (3, 1), (3, 3), (1, 3)])));
        Assert.Equal(new Box(1, 1, 3, 3), PixelSelection.Rectangle(5, 5, 1, 1, 3, 3).Bounds());
        var again = PixelSelection.FromText(PixelSelection.Wand(wand, 1, 0, 0, false).ToText());
        Assert.Equal(PixelSelection.Wand(wand, 1, 0, 0, false).Mask, again.Mask);
    }

    [Fact]
    public void ToolsStayInsideTheSelection()
    {
        var selection = PixelSelection.Rectangle(4, 1, 0, 0, 1, 0);
        Assert.Equal("##..", Run("....", selection, new ImageOp(ImageOps.Stroke, "1", "0", "#FF0000", "0,0 3,0")));
        Assert.Equal("##..", Run("....", selection, new ImageOp(ImageOps.Fill, "#FF0000", "0,0", "0", "1")));
    }

    [Fact]
    public void MoveFlipAndRotateWorkOnTheSelectionOrTheWholePicture()
    {
        var corner = PixelSelection.Rectangle(5, 3, 0, 0, 0, 0);
        Assert.Equal("...../..#../.....", Run("#..../...../.....", corner, new ImageOp(ImageOps.Move, "2,1")));
        Assert.Equal("gb#", Run("#bg", null, new ImageOp(ImageOps.Flip, "h")));
        Assert.Equal("b#g", Run("#bg", PixelSelection.Rectangle(3, 1, 0, 0, 1, 0), new ImageOp(ImageOps.Flip, "h")));
        Assert.Equal("#/b", Run("b/#", null, new ImageOp(ImageOps.Flip, "v")));
        Assert.Equal(".#/.b/.g", Run("#bg/...", null, new ImageOp(ImageOps.Rotate90, "cw")));
        Assert.Equal("g./b./#.", Run("#bg/...", null, new ImageOp(ImageOps.Rotate90, "ccw")));
        Assert.Equal(Run("#bg/...", null, new ImageOp(ImageOps.Rotate90, "cw")), Dump(From("#bg/...").Rotated(90)));
        Assert.Equal("##bb/##bb", Run("#b", null, new ImageOp(ImageOps.Scale, "4", "2")));
        Assert.Equal("##../.##./..##", Run("##/##/##", null, new ImageOp(ImageOps.Skew, "45")));
    }

    [Fact]
    public void CanvasResizeCropAndTrimChangeTheSize()
    {
        Assert.Equal(".../.#./...", Run("#", null, new ImageOp(ImageOps.Resize, "3", "3", "1", "1")));
        Assert.Equal(".../.../..#", Run("#", null, new ImageOp(ImageOps.Resize, "3", "3", "2", "2")));
        Assert.Equal("##", Run("###", null, new ImageOp(ImageOps.Resize, "2", "1", "0", "0")));
        Assert.Equal("##/#.", Run("..../.##./.#../....", null, new ImageOp(ImageOps.Trim)));
        Assert.Equal("##/#.", Run("..../.##./.#../....", PixelSelection.Rectangle(4, 4, 1, 1, 2, 2), new ImageOp(ImageOps.Crop)));
        Assert.Equal("#", Run("#.", null, new ImageOp(ImageOps.Resize, "1", "1", "0", "0")));
    }

    [Fact]
    public void AdjustmentsChangeColoursInsideTheSelectionOnly()
    {
        var red = From("#.");
        red.InvertColours();
        Assert.Equal(new Rgba(0, 255, 255, 255), red.Get(0, 0));
        Assert.Equal(Rgba.Clear, red.Get(1, 0));
        var tone = new PixelBuffer(1, 1);
        tone.Set(0, 0, new Rgba(100, 200, 50, 255));
        tone.Posterize(2);
        Assert.Equal(new Rgba(0, 255, 0, 255), tone.Get(0, 0));
        var hue = From("#");
        hue.Adjust(120, 0, 0, 0);
        Assert.Equal(Green, hue.Get(0, 0));
        var grey = From("#");
        grey.Greyscale(100);
        Assert.Equal(new Rgba(76, 76, 76, 255), grey.Get(0, 0));
        Assert.Equal(".b./b#b/.b.", Run(".../.#./...", null, new ImageOp(ImageOps.Outline, "#0000FF")));
        var flat = new PixelBuffer(2, 1);
        flat.Set(0, 0, new Rgba(128, 128, 128, 255));
        flat.Set(1, 0, new Rgba(128, 128, 128, 255));
        flat.Dither(2);
        Assert.Equal((0, 255), (flat.Get(0, 0).R, flat.Get(1, 0).R));
        Assert.Equal("b#", Run("##", PixelSelection.Rectangle(2, 1, 0, 0, 0, 0), new ImageOp(ImageOps.FillSelection, "#0000FF")));
        Assert.Equal("#b", Run("##", PixelSelection.Rectangle(2, 1, 1, 0, 1, 0), new ImageOp(ImageOps.FillSelection, "#0000FF")));
        Assert.Equal("#.", Run("##", PixelSelection.Rectangle(2, 1, 1, 0, 1, 0), new ImageOp(ImageOps.Clear)));
    }

    [Fact]
    public void PasteBlendsAPictureAndSelectsIt()
    {
        var data = Convert.ToBase64String([255, 0, 0, 255, 0, 0, 255, 255]);
        var state = new ImageState(From("..../..../...."));
        ImageOps.Run(state, new ImageOp(ImageOps.Paste, "1,1", "2,1", data));
        Assert.Equal("..../.#b./....", Dump(state.Picture));
        Assert.Equal(2, Count(state.Selection!));
        ImageOps.Run(state, new ImageOp(ImageOps.Clear));
        Assert.Equal("..../..../....", Dump(state.Picture));
    }

    [Fact]
    public void SymmetryDrawsMirrorCopies()
    {
        Assert.Equal([(1, 1), (3, 1)], Symmetry.Copies([(1, 1)], 5, 5, SymmetryMode.Horizontal).SelectMany(c => c));
        Assert.Equal(4, Symmetry.Copies([(1, 1)], 5, 5, SymmetryMode.Horizontal | SymmetryMode.Vertical).Count);
        Assert.Equal((3, 3), Symmetry.Copies([(1, 1)], 5, 5, SymmetryMode.Horizontal | SymmetryMode.Vertical)[3][0]);
    }

    [Fact]
    public void AnOpSurvivesItsTextFormAndAnUnknownOneIsRefused()
    {
        var op = new ImageOp(ImageOps.Stroke, "3", "1", "#FF0000", "0,0 3,3");
        Assert.Equal(op.ToString(), ImageOp.Parse(op.ToString()).ToString());
        Assert.Throws<InvalidDataException>(() => ImageOps.Run(new ImageState(new PixelBuffer(1, 1)), new ImageOp("bogus")));
    }
}
