using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using G3MLib.DataFile.Models;

namespace G3MStudio.Rooms;

public sealed partial class RoomScene
{
    private sealed partial class Builder(GameMakerRoom room, Func<GameMakerEmbeddedTexture, Bitmap?> pageOf)
    {
        private readonly Dictionary<GameMakerEmbeddedTexture, Bitmap?> _pages = [];
        public readonly List<RoomItem> Items = [];
        public readonly List<RoomMarker> Markers = [];
        public readonly List<Bitmap> Tinted = [];
        private readonly Dictionary<(GameMakerEmbeddedTexture Page, int X, int Y, int W, int H, uint Rgb), Bitmap?> _crops = [];
        public int Skipped;
        public int Notes;
        public uint? Fill;

        // A labelled outline that is not an element: an effect layer, or a layer that scrolls in the game (the editor shows it still).
        public void AddNote(Rect bounds, string label) => Markers.Add(new RoomMarker(bounds, label, null, RoomMarker.MakeCaption(label)));

        public void Add(GameMakerTexturePageItem? tpag, (Rect Source, Rect Dest) cell, object? element = null, int flags = 0, ItemLook? look = null, uint? tint = null)
        {
            if (tpag?.TexturePage is not { } texture) { Skipped++; return; }
            if (!_pages.TryGetValue(texture, out var page)) _pages[texture] = page = pageOf(texture);
            if (page is null) { Skipped++; return; }
            var (source, dest) = cell;
            source = source.Translate(new Vector(tpag.SourceX, tpag.SourceY));
            if (tint is { } rgb && TintedCrop(texture, page, source, rgb) is { } crop) (page, source) = (crop, new Rect(0, 0, crop.PixelSize.Width, crop.PixelSize.Height));
            Items.Add(new RoomItem(page, source, dest, element, flags, look));
        }

        // shortcut: a crop over 4 million pixels is drawn untinted, when a huge tinted picture matters tint it while drawing instead.
        private const int MaxTintPixels = 4_000_000;

        // The crop of the page with every colour channel multiplied by the blend colour (alpha stays; the opacity is applied when drawing). One crop serves all items of the same picture and colour.
        private Bitmap? TintedCrop(GameMakerEmbeddedTexture texture, Bitmap page, Rect source, uint rgb)
        {
            var (x, y, w, h) = ((int)source.X, (int)source.Y, (int)source.Width, (int)source.Height);
            if (page is not WriteableBitmap pixels || w <= 0 || h <= 0 || (long)w * h > MaxTintPixels || x < 0 || y < 0 || x + w > pixels.PixelSize.Width || y + h > pixels.PixelSize.Height) return null;
            if (_crops.TryGetValue((texture, x, y, w, h, rgb), out var made)) return made;
            var crop = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Unpremul);
            int red = (int)(rgb & 0xFF), green = (int)((rgb >> 8) & 0xFF), blue = (int)((rgb >> 16) & 0xFF);
            var row = new byte[w * 4];
            using (var from = pixels.Lock())
            using (var to = crop.Lock())
            {
                for (var line = 0; line < h; line++)
                {
                    Marshal.Copy(from.Address + (y + line) * from.RowBytes + x * 4, row, 0, row.Length);
                    for (var i = 0; i < row.Length; i += 4)
                    {
                        row[i] = (byte)(row[i] * blue / 255);
                        row[i + 1] = (byte)(row[i + 1] * green / 255);
                        row[i + 2] = (byte)(row[i + 2] * red / 255);
                    }

                    Marshal.Copy(row, 0, to.Address + line * to.RowBytes, row.Length);
                }
            }

            Tinted.Add(crop);
            _crops[(texture, x, y, w, h, rgb)] = crop;
            return crop;
        }

        // The blend colour of an element as 0x00BBGGRR, or null when it leaves the picture as it is (never set, or white).
        private static uint? TintOf(uint color) => color == 0 || (color & 0xFFFFFF) == 0xFFFFFF ? null : color & 0xFFFFFF;

        // GameMaker colours are 0xAABBGGRR; a model that never set one (0) is drawn opaque.
        private static double AlphaOf(uint color) => color == 0 ? 1 : (color >> 24) / 255.0;
    }
}
