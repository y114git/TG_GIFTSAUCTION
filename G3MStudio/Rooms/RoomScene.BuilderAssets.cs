using Avalonia;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Localization;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Rooms;

public sealed partial class RoomScene
{
    private sealed partial class Builder
    {
        public void AddSprite(GameMakerSprite? definition, int frame, (double X, double Y, double ScaleX, double ScaleY) at, object? element, float rotation = 0, uint color = 0)
        {
            if (definition is not { Textures.Count: > 0 } sprite) { Skipped++; return; }
            var tpag = sprite.Textures[Math.Abs(frame) % sprite.Textures.Count].Texture;
            if (tpag is null) { Skipped++; return; }
            var look = new ItemLook(rotation, new Point(at.X, at.Y), AlphaOf(color));
            if (sprite.V3NineSlice is { Enabled: true } slice && SliceCells(sprite, tpag, slice, at) is { } cells)
            {
                foreach (var (source, dest, flags, grid) in cells) Add(tpag, (source, dest), element, flags, look with { Grid = grid }, TintOf(color));
                return;
            }

            var whole = new Rect(at.X + (tpag.TargetX - sprite.OriginX) * at.ScaleX, at.Y + (tpag.TargetY - sprite.OriginY) * at.ScaleY, tpag.TargetWidth * at.ScaleX, tpag.TargetHeight * at.ScaleY);
            Add(tpag, (new Rect(0, 0, tpag.SourceWidth, tpag.SourceHeight), whole.Normalize()), element, MirrorOf(at.ScaleX, at.ScaleY), look, TintOf(color));
        }

        // A negative scale mirrors the picture. The rectangle stays upright (so it can be hit, culled and measured) and the mirror is a flag.
        private static int MirrorOf(double scaleX, double scaleY) => (scaleX < 0 ? 1 : 0) | (scaleY < 0 ? 2 : 0);

        // The pieces of a nine-slice sprite drawn at a scale: the corners keep their size, an edge or the middle stretches, or repeats or mirrors its picture at full size along its side.
        // A run of repeated copies is one grid item, so a long edge costs a few items. A mirrored piece is drawn flipped about its own centre.
        // shortcut: a trimmed picture, a flipped scale, a scale of 1 or a size under the borders draws the sprite as one piece.
        private static List<(Rect Source, Rect Dest, int Flags, TileGrid? Grid)>? SliceCells(GameMakerSprite sprite, GameMakerTexturePageItem tpag, GameMakerSprite.NineSlice slice, (double X, double Y, double ScaleX, double ScaleY) at)
        {
            double width = sprite.Width, height = sprite.Height, left = slice.Left, top = slice.Top, right = slice.Right, bottom = slice.Bottom;
            if (at.ScaleX <= 0 || at.ScaleY <= 0 || (at.ScaleX == 1 && at.ScaleY == 1)) return null;
            if (tpag.TargetX != 0 || tpag.TargetY != 0 || tpag.TargetWidth != width || tpag.TargetHeight != height || tpag.SourceWidth != width || tpag.SourceHeight != height) return null;
            if (left < 0 || top < 0 || right < 0 || bottom < 0 || left + right > width || top + bottom > height) return null;
            var (fullW, fullH) = (width * at.ScaleX, height * at.ScaleY);
            if (fullW < left + right || fullH < top + bottom) return null;
            var (x, y) = (at.X - sprite.OriginX * at.ScaleX, at.Y - sprite.OriginY * at.ScaleY);
            double[] sourceX = [0, left, width - right, width], destX = [x, x + left, x + fullW - right, x + fullW];
            double[] sourceY = [0, top, height - bottom, height], destY = [y, y + top, y + fullH - bottom, y + fullH];
            // Tile modes in the file are left, top, right, bottom, centre.
            GameMakerSprite.NineSlice.TileMode Mode(int mode) => slice.TileModes is { } modes && mode < modes.Length ? modes[mode] : GameMakerSprite.NineSlice.TileMode.Stretch;
            var stretch = GameMakerSprite.NineSlice.TileMode.Stretch;
            var cells = new List<(Rect, Rect, int, TileGrid?)>(9);
            for (var row = 0; row < 3; row++)
            {
                for (var column = 0; column < 3; column++)
                {
                    var (index, modeX, modeY) = (row, column) switch { (1, 0) => (0, stretch, Mode(0)), (0, 1) => (1, Mode(1), stretch), (1, 2) => (2, stretch, Mode(2)), (2, 1) => (3, Mode(3), stretch), (1, 1) => (4, Mode(4), Mode(4)), _ => (-1, stretch, stretch) };
                    if (index >= 0 && Mode(index) == GameMakerSprite.NineSlice.TileMode.Hide) continue;
                    var (w, h) = (sourceX[column + 1] - sourceX[column], sourceY[row + 1] - sourceY[row]);
                    if (w <= 0 || h <= 0) continue;
                    foreach (var xs in Runs(sourceX[column], w, destX[column], destX[column + 1] - destX[column], modeX))
                        foreach (var ys in Runs(sourceY[row], h, destY[row], destY[row + 1] - destY[row], modeY))
                            cells.Add((new Rect(xs.Source, ys.Source, xs.SourceLength, ys.SourceLength), new Rect(xs.Dest, ys.Dest, xs.Length, ys.Length), (xs.Mirror ? 1 : 0) | (ys.Mirror ? 2 : 0),
                                (long)xs.Count * ys.Count > 1 ? new TileGrid(xs.Count, ys.Count, xs.Step, ys.Step) : null));
                }
            }

            return cells;
        }

        // One run along an axis: Count copies of a picture of SourceLength, Step apart (a stretched or partial piece is one copy of its own Length).
        private readonly record struct Run(double Source, double SourceLength, double Dest, double Length, int Count, double Step, bool Mirror);

        // Splits a side of a piece that is `length` long and shows a picture `sourceLength` long by its edge mode. Repeat cuts the last copy short, BlankRepeat leaves the remainder empty and Mirror flips every second copy.
        private static List<Run> Runs(double source, double sourceLength, double dest, double length, GameMakerSprite.NineSlice.TileMode mode)
        {
            if (mode is not (GameMakerSprite.NineSlice.TileMode.Repeat or GameMakerSprite.NineSlice.TileMode.Mirror or GameMakerSprite.NineSlice.TileMode.BlankRepeat))
                return [new Run(source, sourceLength, dest, length, 1, length, false)];
            var whole = (int)Math.Floor(length / sourceLength + 1e-9);
            var runs = new List<Run>(3);
            if (mode == GameMakerSprite.NineSlice.TileMode.Mirror)
            {
                if (whole > 0) runs.Add(new Run(source, sourceLength, dest, sourceLength, (whole + 1) / 2, 2 * sourceLength, false));
                if (whole > 1) runs.Add(new Run(source, sourceLength, dest + sourceLength, sourceLength, whole / 2, 2 * sourceLength, true));
            }
            else if (whole > 0) runs.Add(new Run(source, sourceLength, dest, sourceLength, whole, sourceLength, false));

            var rest = length - whole * sourceLength;
            if (rest > 1e-6 && mode != GameMakerSprite.NineSlice.TileMode.BlankRepeat)
            {
                // A flipped copy cut short shows the far end of the picture.
                var flipped = mode == GameMakerSprite.NineSlice.TileMode.Mirror && whole % 2 == 1;
                runs.Add(new Run(flipped ? source + sourceLength - rest : source, rest, dest + whole * sourceLength, rest, 1, rest, flipped));
            }

            return runs;
        }

        public void AddInstance(GameObject o) => AddSprite(o.ObjectDefinition?.Sprite, o.ImageIndex, (o.X, o.Y, o.ScaleX, o.ScaleY), o, o.Rotation, o.Color);

        // A picture repeated over the room from its first copy, or stretched to the room's size. The copies are one item with a grid, drawn where they are seen.
        public void AddPicture(GameMakerTexturePageItem? tpag, double x, double y, bool stretch, (bool H, bool V) tiled, uint color)
        {
            if (tpag is null) { Skipped++; return; }
            double boundW = tpag.BoundingWidth > 0 ? tpag.BoundingWidth : tpag.TargetWidth, boundH = tpag.BoundingHeight > 0 ? tpag.BoundingHeight : tpag.TargetHeight;
            if (boundW < 1 || boundH < 1) return;
            var (scaleX, scaleY) = stretch ? (room.Width / boundW, room.Height / boundH) : (1.0, 1.0);
            double w = boundW * scaleX, h = boundH * scaleY;
            if (!(w >= 1 && h >= 1)) return; // a room with no size stretches the picture to nothing, and a copy under a pixel cannot be told apart
            if (tiled.H) x -= Math.Ceiling(x / w) * w;
            if (tiled.V) y -= Math.Ceiling(y / h) * h;
            var (columns, rows) = (tiled.H ? Math.Max(1, (int)Math.Ceiling((room.Width - x) / w)) : 1, tiled.V ? Math.Max(1, (int)Math.Ceiling((room.Height - y) / h)) : 1);
            Add(tpag, (new Rect(0, 0, tpag.SourceWidth, tpag.SourceHeight), new Rect(x + tpag.TargetX * scaleX, y + tpag.TargetY * scaleY, tpag.TargetWidth * scaleX, tpag.TargetHeight * scaleY)), null, 0, new ItemLook(0, default, AlphaOf(color), (long)columns * rows > 1 ? new TileGrid(columns, rows, w, h) : null), TintOf(color));
        }

        public void AddMarker(object element, string label, int x, int y) => Markers.Add(new RoomMarker(new Rect(x - 16, y - 16, 32, 32), label, element, RoomMarker.MakeCaption(label)));

        // A text item drawn with its font (a font that is not installed falls back to the default one). Without text it stays a box.
        // shortcut: the size comes from the font's line height (or its point size at 96 dpi), not from the glyph sheet, and the box of a turned text is not turned.
        public void AddText(TextItemInstance t, double dx, double dy)
        {
            var (x, y) = (t.X + (int)dx, t.Y + (int)dy);
            if (string.IsNullOrEmpty(t.Text?.Content)) { AddMarker(t, "text", x, y); return; }
            var font = t.Font;
            var size = font is { LineHeight: > 0 } ? font.LineHeight / 1.15 : (font?.EmSize ?? 12) * 96 / 72;
            var family = font?.DisplayName?.Content is { Length: > 0 } name ? new FontFamily(name) : FontFamily.Default;
            var typeface = new Typeface(family, font is { Italic: true } ? FontStyle.Italic : FontStyle.Normal, font is { Bold: true } ? FontWeight.Bold : FontWeight.Normal);
            var rgb = t.Color == 0 ? 0xFFFFFFu : t.Color & 0xFFFFFF;
            var brush = new Avalonia.Media.Immutable.ImmutableSolidColorBrush(Color.FromArgb((byte)(AlphaOf(t.Color) * 255), (byte)rgb, (byte)(rgb >> 8), (byte)(rgb >> 16)));
            var layout = new FormattedText(t.Text.Content, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, Math.Max(1, size), brush)
            {
                TextAlignment = (t.Alignment & 3) switch { 1 => TextAlignment.Center, 2 => TextAlignment.Right, _ => TextAlignment.Left },
                LineHeight = font is { LineHeight: > 0 } ? font.LineHeight + t.LineSpacing : double.NaN,
            };
            if (t.Wrap && t.FrameWidth > 0) layout.MaxTextWidth = t.FrameWidth;
            else if (t.FrameWidth > 0) layout.MaxTextWidth = Math.Max(layout.Width, t.FrameWidth);
            double sx = t.ScaleX == 0 ? 1 : t.ScaleX, sy = t.ScaleY == 0 ? 1 : t.ScaleY;
            var text = new RoomText(layout, new Point(x, y), new Point(t.OriginX, t.OriginY), sx, sy, t.Rotation);
            var bounds = new Rect(x - t.OriginX * sx, y - t.OriginY * sy, Math.Max(1, layout.Width * Math.Abs(sx)), Math.Max(1, layout.Height * Math.Abs(sy)));
            Markers.Add(new RoomMarker(bounds, t.Text.Content, t, null, text));
        }

        public void AddTile(Tile t, double dx, double dy, object? element) => Add(t.Tpag, (new Rect(t.SourceX, t.SourceY, t.Width, t.Height), new Rect(t.X + dx, t.Y + dy, t.Width * t.ScaleX, t.Height * t.ScaleY).Normalize()), element, MirrorOf(t.ScaleX, t.ScaleY), new ItemLook(0, default, AlphaOf(t.Color)), TintOf(t.Color));

        // The layer's own offset moves everything on an asset layer, like layer_x and layer_y in the game.
        public void AddAssets(Layer layer, Layer.LayerAssetsData assets)
        {
            double dx = layer.XOffset, dy = layer.YOffset;
            foreach (var t in assets.LegacyTiles ?? []) AddTile(t, dx, dy, null);
            foreach (var n in assets.NineSlices ?? []) AddSprite(n.Sprite, (int)n.FrameIndex, (n.X + dx, n.Y + dy, n.ScaleX, n.ScaleY), null, n.Rotation, n.Color);
            foreach (var s in assets.Sprites ?? []) AddSprite(s.Sprite, (int)s.FrameIndex, (s.X + dx, s.Y + dy, s.ScaleX, s.ScaleY), s, s.Rotation, s.Color);
            foreach (var q in assets.Sequences ?? []) AddMarker(q, q.Sequence?.Name.Content ?? "sequence", q.X + (int)dx, q.Y + (int)dy);
            foreach (var p in assets.ParticleSystems ?? []) AddMarker(p, p.Name?.Content ?? "particles", p.X + (int)dx, p.Y + (int)dy);
            foreach (var t in assets.TextItems ?? []) AddText(t, dx, dy);
        }

        // A background layer is a colour Fill (no sprite) or a sprite, optionally tiled or stretched, from the layer's offset; its blend colour's alpha fades it.
        public void AddBackgroundLayer(Layer layer, Layer.LayerBackgroundData background)
        {
            if (!background.Visible) return;
            if (layer.HSpeed != 0 || layer.VSpeed != 0) AddNote(new Rect(layer.XOffset, layer.YOffset + 16 * Notes++, 200, 16), Loc.Current.Format("rooms.noteScroll", ("h", layer.HSpeed.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture)), ("v", layer.VSpeed.ToString("0.##", System.Globalization.CultureInfo.CurrentCulture))));
            if (background.Sprite is not { } sprite)
            {
                if (Fill is null && Items.Count == 0) Fill = background.Color & 0xFFFFFF;
                return;
            }

            if (sprite.Textures.Count == 0) { Skipped++; return; }
            var frame = Math.Max(0, (int)Math.Floor(background.FirstFrame)) % sprite.Textures.Count;
            AddPicture(sprite.Textures[frame].Texture, layer.XOffset, layer.YOffset, background.Stretch, (background.TiledHorizontally, background.TiledVertically), background.Color);
        }
    }
}
