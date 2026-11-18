using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using G3MStudio.Explorer;

namespace G3MStudio.Editor;

/// <summary>What a click on a link of a property grid asks for.</summary>
public enum LinkAction { Open, FindUsages, EditImage }

/// <summary>A link of a grid was used and no one near it listens: the window opens the entry. Nested grids (room layers, extensions) reach it this way.</summary>
public sealed class LinkRequestedEventArgs(RoutedEvent routedEvent, LinkAction action, string key) : RoutedEventArgs(routedEvent)
{
    public LinkAction Action { get; } = action;

    public string Key { get; } = key;
}

/// <summary>Shared state of the reference links: the picture cache the pickers and lists draw their previews from, and the event a link raises.</summary>
public sealed class LinkContext
{
    private LinkContext()
    {
    }

    /// <summary>Set once on the window; every control under it reads it.</summary>
    public static readonly AttachedProperty<ThumbnailService?> PicturesProperty = AvaloniaProperty.RegisterAttached<LinkContext, Control, ThumbnailService?>("Pictures", null, inherits: true);

    public static ThumbnailService? GetPictures(Control control) => control.GetValue(PicturesProperty);

    public static void SetPictures(Control control, ThumbnailService? value) => control.SetValue(PicturesProperty, value);

    public static readonly RoutedEvent<LinkRequestedEventArgs> RequestedEvent = RoutedEvent.Register<LinkContext, LinkRequestedEventArgs>("LinkRequested", RoutingStrategies.Bubble);
}
