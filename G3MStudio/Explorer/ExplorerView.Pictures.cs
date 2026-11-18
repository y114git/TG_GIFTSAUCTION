using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Explorer;

public sealed partial class ExplorerView
{
    private static readonly HashSet<string> PictureTypes = new(["Sprites", "Backgrounds", "Fonts", "EmbeddedTextures", "Rooms", "Objects", "GameObjects", "Tilesets", "Paths", "AnimationCurves"], StringComparer.OrdinalIgnoreCase);
    private bool _picturesQueued;

    /// <summary>
    /// An edit, undo or redo changed the data: the shown row pictures that may have changed are asked for again, once per dispatcher pass. A row keeps its old picture until the new one is
    /// ready, and the service keeps the picture when the entry's signature is unchanged, so a row an edit did not touch costs one cheap check.
    /// </summary>
    public void RefreshPictures(IReadOnlyList<ProjectTouch>? touched)
    {
        if (_picturesQueued || Pictures is null || !Thumbnails || (touched is not null && !touched.Any(t => PictureTypes.Contains(t.Type)))) return;
        _picturesQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _picturesQueued = false;
            foreach (var item in List.GetRealizedContainers().OfType<ListBoxItem>())
            {
                if (item.DataContext is not ExplorerRow { IsItem: true } row || !ResourceThumbnails.CanHavePicture(row.Key)) continue;
                if (item.GetVisualDescendants().OfType<Image>().FirstOrDefault(i => i.Name == "Thumb") is not { IsVisible: true } thumb) continue;
                var key = ResourceTypes.KeyOf(row.Key, row.Text);
                if (Pictures.TryGet(key, out var cached))
                {
                    if (cached is not null) ShowPicture(thumb, cached);
                    continue;
                }

                CancelThumb(thumb);
                var source = _pending[thumb] = new CancellationTokenSource();
                _ = LoadThumb(thumb, key, source);
            }
        }, DispatcherPriority.Background);
    }
}
