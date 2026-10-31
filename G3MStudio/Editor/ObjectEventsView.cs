using Avalonia.Controls;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>The events of an object above its properties: each event with the code entries it runs, as links that open the code.</summary>
public sealed class ObjectEventsView : StackPanel
{
    public ObjectEventsView(GameMakerGameObject obj, Action<string> open)
    {
        Name = "ObjectEvents";
        Rows = [.. Collect(obj)];
        foreach (var row in Rows) Children.Add(Row(row, open)); // plain controls: an items control in a docked header got no size in the tab
    }

    /// <summary>Event label and code entry name, in file order.</summary>
    public IReadOnlyList<(string Event, string Code)> Rows { get; }

    private static IEnumerable<(string Event, string Code)> Collect(GameMakerGameObject obj)
    {
        for (var type = 0; type < obj.Events.Count; type++)
        {
            foreach (var evt in obj.Events[type])
            {
                var label = $"{(EventType)type} {evt.EventSubtype}";
                foreach (var action in evt.Actions)
                    if (action.CodeId?.Name?.Content is { } code) yield return (label, code);
            }
        }
    }

    private static Button Row((string Event, string Code) row, Action<string> open)
    {
        var button = new Button { Content = $"{row.Event}  →  {row.Code}", HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left, Classes = { "link" } };
        button.Click += (_, _) => open(ResourceTypes.KeyOf("code", row.Code));
        return button;
    }
}
