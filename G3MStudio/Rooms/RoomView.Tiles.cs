using Avalonia;
using Avalonia.Input;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Rooms;

public enum TileOp { Copy, Cut, Delete, FlipHorizontal, FlipVertical }

/// <summary>Tile selection, the floating paste block and the brush size of the tile tools.</summary>
public sealed partial class RoomView
{
    private (int X, int Y) _floatAt;

    /// <summary>The cells (x, y, width, height) chosen with the tile select tool, or null.</summary>
    public (int X, int Y, int Width, int Height)? TileSelection { get; set { field = value; InvalidateVisual(); } }

    public TileBlock? Floating { get; private set { field = value; InvalidateVisual(); } }

    /// <summary>Cells a paint or erase touches around the pointer cell (1 to 8).</summary>
    public int BrushSize { get; set { field = Math.Clamp(value, 1, 8); InvalidateVisual(); } } = 1;

    public event Action<TileOp>? TileOperation;

    /// <summary>Ctrl+V on a tile layer: the editor answers true after it has called <see cref="StartFloating"/>.</summary>
    public event Func<bool>? TilePasteRequested;

    /// <summary>The floating block was put down with its top-left cell here.</summary>
    public event Action<TileBlock, int, int>? FloatingDropped;

    public void StartFloating(TileBlock block) => Floating = block;

    public void RequestTileOperation(TileOp op) => TileOperation?.Invoke(op);

    public bool RequestTilePaste() => TilePasteRequested?.Invoke() == true;

    /// <summary>The cell under a point even outside the layer (a pasted block may hang over the edge); null without a tile layer.</summary>
    private (int X, int Y)? CellAt(Point room)
    {
        if (TileLayer is not { TilesData.Background: { } set } layer || set.GMS2TileWidth == 0 || set.GMS2TileHeight == 0) return null;
        return ((int)Math.Floor((room.X - layer.XOffset) / set.GMS2TileWidth), (int)Math.Floor((room.Y - layer.YOffset) / set.GMS2TileHeight));
    }

    private bool DropFloating(Point room)
    {
        if (Floating is not { } block || CellAt(room) is not { } cell) return false;
        Floating = null;
        FloatingDropped?.Invoke(block, cell.X, cell.Y);
        return true;
    }

    /// <summary>Keys of tile selection: Ctrl+C, Ctrl+X, Delete, Ctrl+V, Escape and, on a floating block, X and Y to flip it. Returns true when used.</summary>
    private bool TileKeyOperation(KeyEventArgs e)
    {
        if (TileLayer is null) return false;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        if (Floating is { } block)
        {
            switch (e.Key)
            {
                case Key.Escape: Floating = null; return true;
                case Key.X when !ctrl: Floating = TileBlocks.FlipHorizontal(block); return true;
                case Key.Y when !ctrl: Floating = TileBlocks.FlipVertical(block); return true;
            }
        }

        if (TileSelection is not null)
        {
            TileOp? op = e.Key switch { Key.C when ctrl => TileOp.Copy, Key.X when ctrl => TileOp.Cut, Key.Delete => TileOp.Delete, _ => null };
            if (op is { } chosen)
            {
                TileOperation?.Invoke(chosen);
                return true;
            }

            if (e.Key == Key.Escape)
            {
                TileSelection = null;
                return true;
            }
        }

        return e.Key == Key.V && ctrl && RequestTilePaste();
    }

    private IEnumerable<(int X, int Y)> Brushed(IEnumerable<(int X, int Y)> cells) =>
        BrushSize > 1 && Tool is RoomTool.Paint or RoomTool.Erase or RoomTool.Line ? cells.SelectMany(c => TileBlocks.Brush(c, BrushSize)).Distinct() : cells;
}
