using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Explorer;
using G3MStudio.Localization;
using G3MStudio.Rooms;
using G3MStudio.Theme;

namespace G3MStudio.Editor;

/// <summary>
/// The links of an object above its events: sprite, parent object and mask sprite, each with a picture, its name as a link that goes there, a button that picks another target
/// from a searchable list, and a cross that clears it. Beside them, on the left so no gap opens, a picture of the sprite whose width follows a splitter; its play controls sit under the rows. Every change is one undoable step.
/// </summary>
public sealed class ObjectLinksView : Grid
{
    private readonly GameMakerGameObject _object;
    private readonly Workspace _workspace;
    private readonly Action<string> _open, _editImage;
    private readonly StackPanel _rows = new() { Spacing = 6 };
    private readonly Border _preview = new() { Name = "ObjectPreview", MinWidth = 64, MinHeight = 64, BorderThickness = new Thickness(1), VerticalAlignment = VerticalAlignment.Top };
    private readonly ColumnDefinition _previewColumn = new(150, GridUnitType.Pixel) { MinWidth = 64 };
    private readonly SpritePreviewPlayer _player;
    private readonly StackPanel _previewBox = new() { Name = "ObjectPreviewBox", Spacing = 4, VerticalAlignment = VerticalAlignment.Top };
    private readonly TextBlock _message = new() { Name = "ObjectLinksMessage", TextWrapping = Avalonia.Media.TextWrapping.Wrap };
    private readonly TextureBitmaps _textures;
    private string _shown = "";
    private object?[] _pictures = [];

    public ObjectLinksView(GameMakerGameObject obj, Workspace workspace, Action<string> open, Action<string> editImage, TextureBitmaps? textures = null)
    {
        (_object, _workspace, _open, _editImage) = (obj, workspace, open, editImage);
        Name = "ObjectLinks";
        _textures = textures ?? new TextureBitmaps();
        _player = new SpritePreviewPlayer(_textures);
        ColumnDefinitions = [_previewColumn, new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)];
        _preview.Bind(Border.BorderBrushProperty, _preview.GetResourceObservable("Border.Subtle"));
        ToolTip.SetTip(_preview, Loc.Current["editors.object.previewTip"]);
        var left = new StackPanel { Spacing = 6, Margin = new Thickness(4, 0, 0, 0), Children = { _rows, _message, _player } };
        SetColumn(left, 2);
        var splitter = new GridSplitter { Name = "ObjectPreviewSplitter", Width = 5, ResizeDirection = GridResizeDirection.Columns, Background = Avalonia.Media.Brushes.Transparent };
        ToolTip.SetTip(splitter, Loc.Current["fonts.dragResize"]);
        SetColumn(splitter, 1);
        _previewBox.Children.Add(_preview);
        Children.Add(_previewBox);
        Children.Add(splitter);
        Children.Add(left);
        // A square box that follows the width the splitter gives it.
        _preview.SizeChanged += (_, e) =>
        {
            if (double.IsNaN(_preview.Height) || Math.Abs(_preview.Height - e.NewSize.Width) > 0.5) _preview.Height = e.NewSize.Width;
        };
        // In a narrow tab the links need the whole width, so the picture steps aside and comes back with the width the user gave it.
        var kept = 150.0;
        var narrow = false;
        SizeChanged += (_, e) =>
        {
            if ((e.NewSize.Width < 520) == narrow) return;
            narrow = !narrow;
            if (narrow) kept = _previewColumn.ActualWidth > 0 ? _previewColumn.ActualWidth : kept;
            (_previewColumn.Width, _previewColumn.MinWidth) = narrow ? (new GridLength(0), 0) : (new GridLength(kept), 64);
            (_previewBox.IsVisible, splitter.IsVisible, _player.IsVisible) = (!narrow, !narrow, !narrow);
        };
        Fill();
        DetachedFromVisualTree += (_, _) =>
        {
            workspace.History.Changed -= OnChanged;
            workspace.Nicknames.Changed -= OnChanged;
        };
        AttachedToVisualTree += (_, _) =>
        {
            workspace.History.Changed += OnChanged;
            workspace.Nicknames.Changed += OnChanged;
            Fill();
        };
    }

    /// <summary>The row of this link: <c>Sprite</c>, <c>Parent</c> or <c>MaskSprite</c>; for the checks.</summary>
    public Control? RowOf(string kind) => _rows.Children.OfType<Control>().FirstOrDefault(r => r.Name == "ObjectRow_" + kind);

    private void OnChanged() => Fill();

    // The link names the entries, which can be renamed or changed without this object being touched; the object's own name is in the buttons' commands.
    private string Signature() => string.Join('|', _object.Name.Content, _object.Sprite?.Name?.Content, _object.ParentId?.Name?.Content, _object.TextureMaskId?.Name?.Content, _object.Sprite?.Textures.Count, _workspace.Nicknames.Version, _workspace.Locks.Covers(_object.Name.Content ?? ""), Loc.Current.Language);

    // An edit of the sprite's pixels or frames makes a new page image or item, so the preview compares them by reference.
    private object?[] Pictures() => ResourceThumbnails.SpriteOf(_object) is { } sprite
        ? [.. sprite.Textures.SelectMany(t => new object?[] { t?.Texture, t?.Texture?.TexturePage, t?.Texture?.TexturePage?.TextureData?.Image })]
        : [];

    private void Fill()
    {
        var pictures = Pictures();
        var repaint = !pictures.SequenceEqual(_pictures, ReferenceEqualityComparer.Instance);
        if (repaint)
            foreach (var page in _pictures.OfType<GameMakerEmbeddedTexture>().Distinct()) _textures.Forget(page); // decoded before the edit
        _pictures = pictures;
        var signature = Signature();
        if (signature == _shown)
        {
            if (repaint) ShowPreview();
            return;
        }

        _shown = signature;
        _rows.Children.Clear();
        var name = _object.Name.Content ?? "";
        _rows.Children.Add(LinkRow("Sprite", "editors.object.sprite", "sprites", _object.Sprite, key => Set(new SetPropertyCommand("objects", name, "Sprite", key)), new LinkEdit(PictureRefs.KeyOf(_object), "ObjectEditImage", "textureItem.edit")));
        _rows.Children.Add(LinkRow("Parent", "editors.object.parent", "objects", _object.ParentId, key => Set(new ObjectParentCommand(name, ResourceTypes.SplitKey(key)?.Name ?? "")), null));
        _rows.Children.Add(LinkRow("MaskSprite", "editors.object.maskSprite", "sprites", _object.TextureMaskId, key => Set(new SetPropertyCommand("objects", name, "TextureMaskId", key)), new LinkEdit(PictureRefs.MaskKeyOf(_object), "ObjectEditMaskImage", "object.editMaskImage")));
        ShowPreview();
    }

    private void ShowPreview()
    {
        var sprite = ResourceThumbnails.SpriteOf(_object);
        if (sprite is null)
        {
            _player.Bind(null, null);
            _preview.Child = new TextBlock { Name = "ObjectPreviewEmpty", Text = Loc.Current["editors.object.noSprite"], Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap, Margin = new Thickness(6), VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center };
            return;
        }

        // The player draws the first frame itself (with the frame's offsets inside the bounding box), so the cropped thumbnail of the list is not used here.
        var image = new Image { Name = "ObjectPreviewImage", Stretch = Avalonia.Media.Stretch.Uniform, Margin = new Thickness(4), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        Avalonia.Media.RenderOptions.SetBitmapInterpolationMode(image, Avalonia.Media.Imaging.BitmapInterpolationMode.None);
        _preview.Child = image;
        _player.Bind(sprite, image);
        _player.ShowFirst();
    }

    /// <summary>The edit-picture button of a link row: the key of the picture it opens (null when the link has none), the control name and the label key.</summary>
    private sealed record LinkEdit(string? PictureKey, string Name, string LabelKey);

    private WrapPanel LinkRow(string kind, string labelKey, string type, GameMakerNamedResource? target, Action<string> set, LinkEdit? edit)
    {
        var locked = _workspace.Locks.Covers(_object.Name.Content ?? "");
        var row = new WrapPanel { Name = "ObjectRow_" + kind, ItemSpacing = 8, LineSpacing = 4 };
        row.Children.Add(new TextBlock { Text = Loc.Current[labelKey], Width = 90, VerticalAlignment = VerticalAlignment.Center, TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis });
        var key = target?.Name?.Content is { Length: > 0 } targetName ? ResourceTypes.KeyOf(type, targetName) : null;
        if (key is not null)
        {
            row.Children.Add(LinkThumb.Make(key, "ObjectThumb_" + kind));
            var link = new Button { Name = "ObjectOpen_" + kind, Content = new TextBlock { Text = _workspace.Nicknames.Label(key), TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis }, Classes = { "link" }, MaxWidth = 200 };
            HoverCards.Attach(link, key, key);
            link.Click += (_, _) => _open(key);
            row.Children.Add(link);
        }
        else
        {
            row.Children.Add(new TextBlock { Name = "ObjectNone_" + kind, Text = Loc.Current[kind == "Parent" ? "editors.object.noParent" : "link.none"], Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center });
        }

        var change = new Button { Name = "ObjectSet_" + kind, Content = Loc.Current[key is null ? "link.set" : "link.change"] };
        change.Click += (_, _) => ResourcePicker.Show(change, _workspace, type, key, set);
        row.Children.Add(change.Gate(locked ? Loc.Current["link.locked"] : null));
        if (key is not null)
        {
            var clear = new Button { Name = "ObjectClear_" + kind, Content = G3MStudio.Theme.GlyphIcons.Close(), Classes = { "icon" } }.Labeled("link.clear");
            clear.Click += (_, _) => set("");
            row.Children.Add(clear.Gate(locked ? Loc.Current["link.locked"] : null));
        }

        if (edit is not null)
        {
            var editButton = EditorButtons.EditImage(edit.Name, () => _editImage(edit.PictureKey ?? ""));
            editButton.Content = Loc.Current[edit.LabelKey];
            row.Children.Add(editButton.Gate(edit.PictureKey is null ? Loc.Current["editors.object.noSpriteToEdit"] : null));
        }

        return row;
    }

    private void Set(EditCommand command)
    {
        try
        {
            _workspace.Edit(command);
            _message.Text = "";
        }
        catch (InvalidOperationException e)
        {
            _message.Text = e.Message;
        }
    }
}
