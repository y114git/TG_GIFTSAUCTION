using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class TileBlockTests
{
    private const uint Mirror = 1u << 28;
    private const uint Flip = 1u << 29;
    private const uint Rotate = 1u << 30;

    private static uint[][] Rows() => [[1, 2, 3, 4], [5, 6, 7, 8], [9, 10, 11, 12]];

    [Fact]
    public void CopyTakesTheRectangleAndClipsToTheLayer()
    {
        var block = TileBlocks.Copy(Rows(), 1, 1, 2, 2);
        Assert.Equal((2, 2), (block.Width, block.Height));
        Assert.Equal([6u, 7, 10, 11], block.Cells);
        var clipped = TileBlocks.Copy(Rows(), 3, 2, 5, 5);
        Assert.Equal((1, 1), (clipped.Width, clipped.Height));
        Assert.Equal([12u], clipped.Cells);
        Assert.Empty(TileBlocks.Copy(Rows(), 10, 10, 2, 2).Cells);
    }

    [Fact]
    public void FlipReversesTheCellsAndTurnsTheFlags()
    {
        var block = new TileBlock(2, 2, [1, 2, 0, 4]);
        Assert.Equal([2 | Mirror, 1 | Mirror, 4 | Mirror, 0], TileBlocks.FlipHorizontal(block).Cells);
        Assert.Equal([0, 4 | Flip, 1 | Flip, 2 | Flip], TileBlocks.FlipVertical(block).Cells);
        Assert.Equal(block.Cells, TileBlocks.FlipHorizontal(TileBlocks.FlipHorizontal(block)).Cells);
    }

    [Fact]
    public void AQuarterTurnedTileFlipsItsOtherFlag()
    {
        Assert.Equal(7 | Rotate | Flip, TileBlocks.Reflect(7 | Rotate, true));
        Assert.Equal(7 | Rotate | Mirror, TileBlocks.Reflect(7 | Rotate, false));
        Assert.Equal(7u, TileBlocks.Reflect(7 | Mirror, true));
    }

    [Fact]
    public void PasteSkipsEmptyCellsAndCellsOutsideAndUnchangedOnes()
    {
        var block = new TileBlock(2, 2, [99, 0, 7, 100]);
        var changes = TileBlocks.Paste(Rows(), block, 3, 1);
        Assert.Equal([(3, 1, 99u), (3, 2, 7u)], changes);
        Assert.Equal([(0, 0, 99u), (0, 1, 7u), (1, 1, 100u)], TileBlocks.Paste(Rows(), block, 0, 0));
        Assert.Contains((1, 0, 0u), TileBlocks.Paste(Rows(), block, 0, 0, withEmpty: true));
    }

    [Fact]
    public void ClearEmptiesOnlyCellsThatHoldATile()
    {
        var rows = Rows();
        rows[0][0] = 0;
        Assert.Equal([(1, 0, 0u), (0, 1, 0u), (1, 1, 0u)], TileBlocks.Clear(rows, 0, 0, 2, 2));
    }

    [Fact]
    public void FlipInPlaceMirrorsTheSelectionOnTheLayer()
    {
        var changes = TileBlocks.FlipInPlace(Rows(), 0, 0, 3, 1, true);
        Assert.Equal([(0, 0, 3u | Mirror), (1, 0, 2u | Mirror), (2, 0, 1u | Mirror)], changes);
    }

    [Fact]
    public void LineIncludesBothEndsAndStepsOneCellAtATime()
    {
        Assert.Equal([(0, 0), (1, 0), (2, 1), (3, 1)], TileBlocks.Line((0, 0), (3, 1)));
        Assert.Equal([(2, 2)], TileBlocks.Line((2, 2), (2, 2)));
        var back = TileBlocks.Line((4, 4), (1, 1));
        Assert.Equal((4, 4), back[0]);
        Assert.Equal((1, 1), back[^1]);
        Assert.Equal(4, back.Count);
    }

    [Fact]
    public void BrushIsASquareAroundTheCell()
    {
        Assert.Equal([(5, 5)], TileBlocks.Brush((5, 5), 1));
        Assert.Equal(9, TileBlocks.Brush((5, 5), 3).Count);
        Assert.Contains((4, 4), TileBlocks.Brush((5, 5), 3));
        Assert.Equal([(5, 5), (6, 5), (5, 6), (6, 6)], TileBlocks.Brush((5, 5), 2));
        Assert.Equal((1, 2, 3, 2), TileBlocks.Rectangle((3, 3), (1, 2)));
    }
}
