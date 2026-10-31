using System.Globalization;
using System.Text;

namespace G3MStudio.Core.Workspace;

/// <summary>Tile layer data as CSV: one line per row of cells, each cell the tile value (0 is empty; the flags are part of the number).</summary>
public static class TileCsv
{
    public static string Write(uint[][] rows)
    {
        var text = new StringBuilder();
        foreach (var row in rows) text.AppendLine(string.Join(',', row.Select(v => v.ToString(CultureInfo.InvariantCulture))));
        return text.ToString();
    }

    /// <summary>The cells of <paramref name="rows"/> that the CSV changes. The CSV must have as many rows and columns as the layer.</summary>
    public static List<(int X, int Y, uint Value)> Changes(uint[][] rows, string csv)
    {
        var lines = csv.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length != rows.Length) throw new InvalidDataException($"The CSV has {lines.Length} rows; the layer has {rows.Length}.");
        var changes = new List<(int, int, uint)>();
        for (var y = 0; y < rows.Length; y++)
        {
            var cells = lines[y].Split(',');
            if (cells.Length != rows[y].Length) throw new InvalidDataException($"Row {y + 1} has {cells.Length} cells; the layer has {rows[y].Length}.");
            for (var x = 0; x < cells.Length; x++)
            {
                if (!uint.TryParse(cells[x].Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value)) throw new InvalidDataException($"Row {y + 1}, column {x + 1}: '{cells[x].Trim()}' is not a tile value.");
                if (value != rows[y][x]) changes.Add((x, y, value));
            }
        }

        return changes;
    }
}
