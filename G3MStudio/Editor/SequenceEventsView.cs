using System.Collections;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Layout;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

/// <summary>
/// The moments and broadcast messages of a sequence as two lists: the frame, the text (one function name or message per line) and a Delete button
/// for each, and an Add button with a frame box. Every change is one undo step.
/// </summary>
public sealed class SequenceEventsView : StackPanel
{
    private readonly GameMakerSequence _sequence;
    private readonly Workspace _workspace;
    private bool _filling;

    public SequenceEventsView(GameMakerSequence sequence, Workspace workspace)
    {
        (_sequence, _workspace) = (sequence, workspace);
        Name = "SequenceEvents";
        Spacing = 4;
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Fill;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Fill;
        Fill();
    }

    private string SequenceName => _sequence.Name.Content ?? "";

    private void Fill()
    {
        _filling = true;
        Children.Clear();
        Section(SequenceEventCommand.Moments, "sequences.moments", "editors.sequence.addMoment", _sequence.Moments);
        Section(SequenceEventCommand.Messages, "sequences.broadcast", "editors.sequence.addMessage", _sequence.BroadcastMessages);
        _filling = false;
    }

    private void Section(string list, string titleKey, string addKey, IList? items)
    {
        Children.Add(new TextBlock { Text = Loc.Current[titleKey], FontWeight = Avalonia.Media.FontWeight.SemiBold });
        foreach (var (item, i) in (items ?? Array.Empty<object>()).Cast<object>().Select((item, i) => (item, i))) Children.Add(Row(list, i, item));
        var frame = new NumericUpDown { Name = "NewFrame_" + list, Minimum = 0, Maximum = 10_000_000, FormatString = "0", Value = NextFrame(items), MinWidth = 100 };
        var add = new Button { Name = "Add_" + list, Content = Loc.Current[addKey] };
        add.Click += (_, _) => TryEdit(new SequenceEventCommand(SequenceName, list, SequenceEventCommand.Add, -1, (float)(frame.Value ?? 0)));
        Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, Children = { frame, add } });
    }

    private static decimal NextFrame(IList? items) => items is null || items.Count == 0 ? 0 : items.Cast<object>().Max(k => (decimal)KeyOf(k)) + 1;

    private static float KeyOf(object keyframe, string name = "Key") => (float)(keyframe.GetType().GetProperty(name)?.GetValue(keyframe) ?? 0f);

    private DockPanel Row(string list, int index, object keyframe)
    {
        var key = KeyOf(keyframe, "Key");
        var frame = new NumericUpDown { Name = $"{list}_Frame_{index}", Minimum = 0, Maximum = 10_000_000, FormatString = "0", Value = (decimal)key, MinWidth = 100 };
        frame.ValueChanged += (_, e) =>
        {
            if (!_filling && e.NewValue is { } v && (float)v != key) TryEdit(new SetPropertyCommand("sequences", SequenceName, $"{list}[{index}].Key", ((float)v).ToString("R", CultureInfo.InvariantCulture)));
        };
        var text = new TextBox { Name = $"{list}_Text_{index}", AcceptsReturn = true, MinWidth = 200, Text = string.Join('\n', Lines(keyframe)) };
        text.LostFocus += (_, _) =>
        {
            if (!_filling && (text.Text ?? "").Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n') != string.Join('\n', Lines(keyframe))) TryEdit(new SequenceEventCommand(SequenceName, list, SequenceEventCommand.SetText, index, 0, text.Text ?? ""));
        };
        var delete = new Button { Name = $"{list}_Delete_{index}", Content = Loc.Current["timelines.remove"] };
        delete.Click += (_, _) => TryEdit(new SequenceEventCommand(SequenceName, list, SequenceEventCommand.Delete, index));
        DockPanel.SetDock(frame, Avalonia.Controls.Dock.Left);
        DockPanel.SetDock(delete, Avalonia.Controls.Dock.Right);
        return new DockPanel { Children = { frame, delete, text } };
    }

    private static IEnumerable<string> Lines(object keyframe) => keyframe switch
    {
        GameMakerSequence.Keyframe<GameMakerSequence.Moment> m => (m.Channels.FirstOrDefault()?.Value.Events ?? []).Select(s => s.Content ?? ""),
        GameMakerSequence.Keyframe<GameMakerSequence.BroadcastMessage> b => (b.Channels.FirstOrDefault()?.Value.Messages ?? []).Select(s => s.Content ?? ""),
        _ => [],
    };

    private void TryEdit(EditCommand command)
    {
        try
        {
            _workspace.Edit(command);
        }
        catch (InvalidOperationException)
        {
            Fill(); // refused (a frame that is taken): show the real values again
        }
    }
}
