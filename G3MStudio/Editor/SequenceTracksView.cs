using Avalonia;
using G3MStudio.Theme;
using Avalonia.Controls;
using Avalonia.Media;
using G3MLib.DataFile.Models;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;
/// <summary>The tracks of a sequence as an indented tree (name, kind), after a line with its length, speed and moment and message counts.</summary>
public sealed class SequenceTracksView : StackPanel
{
    private readonly GameMakerSequence _sequence;
    private readonly Workspace? _workspace;

    public SequenceTracksView(GameMakerSequence sequence, Workspace? workspace = null)
    {
        (_sequence, _workspace) = (sequence, workspace);
        Name = "SequenceTracks";
        Lines = [];
        if (workspace is not null)
        {
            AttachedToVisualTree += (_, _) => workspace.History.Changed += Rebuild;
            DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Rebuild;
        }

        Rebuild();
    }

    private void Rebuild()
    {
        var (sequence, workspace) = (_sequence, _workspace);
        var lines = new List<string>();
        foreach (var track in sequence.Tracks ?? []) Add(lines, track, 0);
        Lines = lines;
        var name = sequence.Name.Content ?? "";
        if (workspace is null)
        {
            ItemsSource = lines;
            return;
        }

        ItemsSource = ((sequence.Tracks ?? []).Select((track, i) =>
        {
            var expander = ParticleEmittersView.Expandable(track.Name?.Content + "  " + track.ModelName?.Content, workspace, "sequences", name, $"Tracks[{i}]");
            var remove = new Button { Name = "TrackRemove_" + i, Content = Loc.Current["timelines.remove"] }.NamedFor("timelines.remove", track.Name?.Content + " " + track.ModelName?.Content);
            remove.Click += (_, _) => workspace.Edit(new SequenceTrackCommand(name, SequenceTrackCommand.Remove, i));
            var header = new DockPanel { Children = { new TextBlock { Text = track.Name?.Content + "  " + track.ModelName?.Content, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center }, remove } };
            DockPanel.SetDock(remove, Avalonia.Controls.Dock.Right);
            expander.Header = header;
            return (object)expander;
        }).Append(AddGroupButton(workspace, name)));
    }

    private static Button AddGroupButton(Workspace workspace, string sequence)
    {
        var add = new Button { Name = "TrackAddGroup", Content = Loc.Current["sequences.addGroup"] };
        add.Click += (_, _) => workspace.Edit(new SequenceTrackCommand(sequence, SequenceTrackCommand.AddGroup, -1, Loc.Current["sequences.newGroup"]));
        return add;
    }

    public IReadOnlyList<string> Lines { get; private set; }

    private static void Add(List<string> lines, GameMakerSequence.Track track, int depth)
    {
        lines.Add(new string(' ', depth * 3) + track.Name?.Content + "  " + track.ModelName?.Content);
        foreach (var child in track.Tracks ?? []) Add(lines, child, depth + 1);
    }

    // The list is built from plain controls: an items control in a docked header got no size in the tab.
    private IEnumerable<object> ItemsSource
    {
        set
        {
            Children.Clear();
            foreach (var item in value) Children.Add(item as Control ?? new TextBlock { Text = item.ToString() });
        }
    }
}
/// <summary>One step box and a Remove button per moment of a timeline, in file order, and an Add Moment button. Every change is one undo step.</summary>
public sealed class TimelineStepsView : StackPanel
{
    private readonly GameMakerTimeline _timeline;
    private readonly Workspace _workspace;
    private bool _filling;

    private readonly Action<string> _open;

    public TimelineStepsView(GameMakerTimeline timeline, Workspace workspace, Action<string> open)
    {
        (_timeline, _workspace, _open) = (timeline, workspace, open);
        Name = "TimelineSteps";
        Spacing = 2;
        AttachedToVisualTree += (_, _) => workspace.History.Changed += Fill;
        DetachedFromVisualTree += (_, _) => workspace.History.Changed -= Fill;
        Fill();
    }

    private string TimelineName => _timeline.Name.Content ?? "";

    // Duplicate in this timeline, or copy or cut to another one: a menu of the timelines.
    private StackPanel MomentTools(int index)
    {
        var duplicate = new Button { Name = "MomentDuplicate_" + index, Content = Loc.Current["editors.timeline.duplicate"] };
        duplicate.Click += (_, _) => CopyMoment(index, TimelineName, false);
        return new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Children = { duplicate, MomentMenuButton(index, false), MomentMenuButton(index, true) } };
    }

    // The window asks whether a copy shares the code entries or gets code of its own, and remembers the answer when asked to.
    private void CopyMoment(int index, string target, bool cut)
    {
        if (TopLevel.GetTopLevel(this) is MainWindow main) _ = main.CopyTimelineMomentAsync(_workspace, TimelineName, index, target, cut);
        else _workspace.Edit(new TimelineMomentCopyCommand(TimelineName, index, target, cut));
    }

    private Button MomentMenuButton(int index, bool cut)
    {
        var button = new Button { Name = (cut ? "MomentCut_" : "MomentCopy_") + index, Content = Loc.Current[cut ? "editors.timeline.cutTo" : "editors.timeline.copyTo"] };
        button.Click += (_, _) =>
        {
            var menu = new MenuFlyout();
            foreach (var other in _workspace.Data.Timelines.Where(t => t != _timeline).Take(200))
            {
                var item = new MenuItem { Header = other.Name.Content };
                var target = other.Name.Content ?? "";
                item.Click += (_, _) => CopyMoment(index, target, cut);
                menu.Items.Add(item);
            }

            if (menu.Items.Count > 0) menu.ShowAt(button);
        };
        return button;
    }

    // Moves all moments from a step on by some steps, as one undo step.
    private StackPanel ShiftRow()
    {
        var from = new NumericUpDown { Name = "ShiftFrom", Minimum = 0, Maximum = 1_000_000, FormatString = "0", Value = 0, MinWidth = 100 };
        var by = new NumericUpDown { Name = "ShiftBy", Minimum = -1_000_000, Maximum = 1_000_000, FormatString = "0", Value = 1, MinWidth = 100 };
        var shift = new Button { Name = "MomentShift", Content = Loc.Current["editors.timeline.shift"] };
        shift.Click += (_, _) => _workspace.Edit(new TimelineShiftCommand(TimelineName, (uint)(from.Value ?? 0), (int)(by.Value ?? 0)));
        var label = new TextBlock { Text = Loc.Current["editors.timeline.shiftFrom"], VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        return new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Children = { label, from, by, shift } };
    }

    private void Fill()
    {
        _filling = true;
        Children.Clear();
        for (var i = 0; i < _timeline.Moments.Count; i++)
        {
            var (index, moment) = (i, _timeline.Moments[i]);
            var box = new NumericUpDown { Name = "MomentStep_" + i, Minimum = 0, Maximum = 1_000_000, FormatString = "0", Value = moment.Step };
            box.ValueChanged += (_, e) =>
            {
                if (!_filling && e.NewValue is { } value && (uint)value != moment.Step) _workspace.Edit(new SetPropertyCommand("timelines", TimelineName, $"Moments[{index}].Step", ((uint)value).ToString(System.Globalization.CultureInfo.InvariantCulture)));
            };
            var remove = new Button { Name = "MomentRemove_" + i, Content = Loc.Current["timelines.remove"] }.NamedFor("timelines.remove", Loc.Current.Format("timelines.moment", ("number", i + 1)));
            remove.Click += (_, _) => _workspace.Edit(new TimelineMomentCommand(TimelineName, TimelineMomentCommand.Remove, index));
            var tools = MomentTools(index);
            DockPanel.SetDock(tools, Avalonia.Controls.Dock.Right);
            var label = new TextBlock { Text = Loc.Current.Format("timelines.moment", ("number", i + 1)), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            DockPanel.SetDock(label, Avalonia.Controls.Dock.Left);
            DockPanel.SetDock(remove, Avalonia.Controls.Dock.Right);
            Children.Add(new DockPanel { Children = { label, remove, tools, box } });

            // The actions of the moment: each runs a code entry; Add action takes the name of an existing code entry.
            for (var a = 0; a < (moment.Event?.Count ?? 0); a++)
            {
                var actionIndex = a;
                var removeAction = new Button { Name = $"ActionRemove_{i}_{a}", Content = Loc.Current["timelines.remove"] }.NamedFor("timelines.remove", Loc.Current.Format("timelines.moment", ("number", i + 1)) + " " + (moment.Event?[a].CodeId?.Name?.Content ?? "?"));
                removeAction.Click += (_, _) => _workspace.Edit(new TimelineActionCommand(TimelineName, TimelineActionCommand.Remove, index, actionIndex));
                var code = moment.Event?[a].CodeId?.Name?.Content ?? "?";
                // The name opens the code entry the action runs.
                var codeName = new Button { Content = code, Classes = { "link" }, Margin = new Thickness(24, 0, 8, 0), HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left };
                codeName.Click += (_, _) => _open(ResourceTypes.KeyOf("code", code));
                DockPanel.SetDock(removeAction, Avalonia.Controls.Dock.Right);
                Children.Add(new DockPanel { Children = { removeAction, codeName } });
            }

            var newCode = new TextBox { Name = "ActionCode_" + i, PlaceholderText = Loc.Current["timelines.actionCode"], Margin = new Thickness(24, 0, 8, 0) };
            var addAction = new Button { Name = "ActionAdd_" + i, Content = Loc.Current["timelines.addAction"] };
            addAction.Click += (_, _) =>
            {
                if (newCode.Text is { Length: > 0 } text && _workspace.Data.Code.Any(c => c.Name.Content == text)) _workspace.Edit(new TimelineActionCommand(TimelineName, TimelineActionCommand.Add, index, -1, text));
            };
            DockPanel.SetDock(addAction, Avalonia.Controls.Dock.Right);
            Children.Add(new DockPanel { Children = { addAction, newCode } });
        }

        Children.Add(ShiftRow());
        var add = new Button { Name = "MomentAdd", Content = Loc.Current["timelines.add"] };
        add.Click += (_, _) => _workspace.Edit(new TimelineMomentCommand(TimelineName, TimelineMomentCommand.Add, -1, _timeline.Moments.Select(m => m.Step + 1).DefaultIfEmpty(0u).Max()));
        Children.Add(add);
        _filling = false;
    }
}

/// <summary>The channels of an animation curve drawn as lines through their points, one colour per channel, with a legend.</summary>
public sealed class AnimationCurveView : Control
{
    private static readonly IBrush[] Colors = [Brushes.DeepSkyBlue, Brushes.Orange, Brushes.LightGreen, Brushes.Violet, Brushes.Khaki, Brushes.Salmon];
    private readonly GameMakerAnimationCurve _curve;

    public AnimationCurveView(GameMakerAnimationCurve curve)
    {
        _curve = curve;
        Name = "AnimationCurveView";
        Height = 260;
    }

    public override void Render(DrawingContext context)
    {
        context.FillRectangle(G3MStudio.Theme.CanvasTheme.Bg, new Rect(Bounds.Size));
        var all = _curve.Channels.SelectMany(c => c.Points).ToList();
        if (all.Count == 0) return;
        var (minX, maxX) = (all.Min(p => p.X), all.Max(p => p.X));
        var (minY, maxY) = (all.Min(p => p.Value), all.Max(p => p.Value));
        double Span(float low, float high) => high - low < 1e-6 ? 1 : high - low;
        var (spanX, spanY) = (Span(minX, maxX), Span(minY, maxY));
        Point Map(float x, float y) => new(44 + (x - minX) / spanX * Math.Max(1, Bounds.Width - 68), Bounds.Height - 24 - (y - minY) / spanY * Math.Max(1, Bounds.Height - 48));
        var axis = new Pen(CanvasTheme.Soft(CanvasTheme.Handle, 0.25), 1);
        var grid = new Pen(CanvasTheme.Soft(CanvasTheme.Handle, 0.08), 1);
        for (var q = 1; q < 4; q++)
        {
            context.DrawLine(grid, Map(minX, minY + (float)(spanY * q / 4)), Map(maxX, minY + (float)(spanY * q / 4)));
            context.DrawLine(grid, Map(minX + (float)(spanX * q / 4), minY), Map(minX + (float)(spanX * q / 4), maxY));
        }

        context.DrawLine(axis, Map(minX, minY), Map(maxX, minY));
        context.DrawLine(axis, Map(minX, minY), Map(minX, maxY));
        void Label(float value, Point at)
        {
            var text = new FormattedText(value.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture), System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 11, CanvasTheme.Muted);
            context.DrawText(text, at);
        }

        Label(maxY, new Point(2, Map(minX, maxY).Y - 7));
        Label(minY, new Point(2, Map(minX, minY).Y - 7));
        Label(minX, new Point(Map(minX, minY).X - 4, Bounds.Height - 16));
        Label(maxX, new Point(Map(maxX, minY).X - 12, Bounds.Height - 16));
        for (var c = 0; c < _curve.Channels.Count; c++)
        {
            var brush = Colors[c % Colors.Length];
            var points = _curve.Channels[c].Points.OrderBy(p => p.X).ToList();
            for (var i = 1; i < points.Count; i++) context.DrawLine(new Pen(brush, 2), Map(points[i - 1].X, points[i - 1].Value), Map(points[i].X, points[i].Value));
            foreach (var p in points) context.DrawEllipse(brush, null, Map(p.X, p.Value), 3, 3);
            var label = new FormattedText(_curve.Channels[c].Name?.Content ?? "", System.Globalization.CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, 12, brush);
            context.DrawText(label, new Point(48 + c * 110, 4));
        }
    }
}
