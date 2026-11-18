using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Media;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Explorer;

/// <summary>
/// The shared side of the hover cards (item 40): the setting, the lookup of what a key stands for and the picture loader, set once by the main window. Every surface (Explorer, code hover, Go To, lists, tabs)
/// puts a card on its control through <see cref="Attach"/> and never builds one itself.
/// </summary>
public static class HoverCards
{
    /// <summary>"Show previews and IDs on hover"; off leaves every card as its plain text and renders nothing more.</summary>
    public static bool Enabled { get; set; } = true;

    /// <summary>What a key stands for in the open file (null: nothing, or no file).</summary>
    public static Func<string, ResourceHoverInfo?>? Describe { get; set; }

    /// <summary>The picture of a key, cached and made off the UI thread; the token stops the wait when the pointer leaves.</summary>
    public static Func<string, CancellationToken, Task<IImage?>>? Picture { get; set; }

    /// <summary>The Explorer key of the resource a name in code stands for (null: none); for the completion list, which knows names only.</summary>
    public static Func<string, string?>? KeyOfName { get; set; }

    private static WeakReference<object>? _owner;

    /// <summary>Sets the three lookups for a window; <see cref="Release"/> takes them away when it closes.</summary>
    public static void Bind(object owner, Func<string, ResourceHoverInfo?> describe, Func<string, string?> keyOfName, Func<string, CancellationToken, Task<IImage?>> picture)
    {
        (Describe, KeyOfName, Picture, _owner) = (describe, keyOfName, picture, new WeakReference<object>(owner));
    }

    /// <summary>Takes the lookups away when they are the ones of this window (another window may have set its own since).</summary>
    public static void Release(object owner)
    {
        if (_owner is null || !_owner.TryGetTarget(out var current) || !ReferenceEquals(current, owner)) return;
        (Describe, KeyOfName, Picture, _owner) = (null, null, null, null);
    }

    /// <summary>The type and the ID on one line: "Sprite, ID 12", or the type alone when the entry has no ID.</summary>
    public static string MetaOf(ResourceHoverInfo info) =>
        info.Id is { } id ? Loc.Current.Format("hoverCard.typeId", ("type", TypeName(info.Type)), ("id", id)) : TypeName(info.Type);

    // Types that cannot be created have no singular name of their own, so they use the name of their list.
    public static string TypeName(string type) => Loc.Current["explorer.typeOne." + type] is var one && one != "explorer.typeOne." + type ? one : Loc.Current["explorer.type." + type];

    /// <summary>
    /// Gives a control the hover card of a key. <paramref name="text"/> is all the card says when it has nothing more to show (setting off, a null key, a key that is no resource);
    /// <paramref name="more"/> is a text the surface adds under the facts of the resource. The control holds a small <see cref="HoverTip"/>; the card is built when the tip opens.
    /// </summary>
    public static void Attach(Control control, string? key, string text, string more = "")
    {
        EnsureTemplate();
        // no key and no text: nothing to say, and an empty card would show as a bare bordered box
        ToolTip.SetTip(control, key is null && string.IsNullOrEmpty(text) ? null : new HoverTip(key, text, more));
    }

    /// <summary>The card itself, for a surface that shows content of its own and no tip (the completion list).</summary>
    public static ResourceHoverCard Card(string key, string text) => new(new HoverTip(key, text));

    private static Application? _templatedFor;

    // A tip that is not a control is shown through the data templates of the application; one template turns every HoverTip into its card.
    private static void EnsureTemplate()
    {
        if (Application.Current is not { } app || ReferenceEquals(app, _templatedFor)) return;
        app.DataTemplates.Add(new FuncDataTemplate<HoverTip>((tip, _) => tip is null ? null : new ResourceHoverCard(tip)));
        _templatedFor = app;
    }
}

/// <summary>What a control asks its hover card to show: the key of a resource (null for plain text), the plain text, and a text to add under the facts.</summary>
public sealed record HoverTip(string? Key, string Text, string More = "")
{
    public override string ToString() => Text;
}
