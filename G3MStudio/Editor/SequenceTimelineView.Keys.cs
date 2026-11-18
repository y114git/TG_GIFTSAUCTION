using Avalonia;
using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

public sealed partial class SequenceTimelineView
{
    /// <summary>The whole-frame move for a drag of <paramref name="frames"/> frames: the key snaps to the edges of the other keyframes of the row and to the playhead.</summary>
    public int SnappedDelta(int row, int frame, double frames)
    {
        if (row < 0 || row >= Rows.Count || Rows[row].Frames.FirstOrDefault(f => f.Index == frame) is not { } target) return (int)Math.Round(frames);
        var edges = Rows[row].Frames.Where(f => f.Index != frame).SelectMany(f => new[] { f.Key, f.Key + f.Length }).Append((float)Playhead);
        var reach = (float)(6 / Scale);
        var snapped = SequenceSnap.Snap((float)(target.Key + frames), edges, reach);
        return (int)Math.Round(snapped - target.Key);
    }

    /// <summary>Adds, splits or deletes at the playhead or on the keyframe under the pointer, each as one undoable edit. Returns false when the edit was refused.</summary>
    public bool Edit(int row, string operation, int index, float frame)
    {
        if (_workspace is null || row < 0 || row >= Rows.Count) return false;
        try
        {
            _workspace.Edit(new SequenceKeyframeCommand(_sequence.Name.Content ?? "", Rows[row].Path, operation, index, frame));
            return true;
        }
        catch (InvalidOperationException e)
        {
            Refused?.Invoke(e.Message);
            return false;
        }
    }

    /// <summary>Raised with the reason when a keyframe edit is refused.</summary>
    public event Action<string>? Refused;

    private void ShowMenu(Point at)
    {
        if (_workspace is null) return;
        var rowIndex = (int)Math.Floor((at.Y - RulerHeight) / RowHeight);
        if (rowIndex < 0 || rowIndex >= Rows.Count || !Rows[rowIndex].Path.EndsWith("Keyframes.List", StringComparison.Ordinal)) return;
        var hit = HitTest(at) is var (row, index) && row == rowIndex ? index : -1;
        var frame = (float)Math.Round(Playhead);
        var menu = new MenuFlyout();
        void Item(string key, string name, Action action, bool enabled = true)
        {
            var item = new MenuItem { Header = Loc.Current[key], Name = name, IsEnabled = enabled };
            item.Click += (_, _) => action();
            menu.Items.Add(item);
        }

        Item("editors.sequence.addKey", "KeyAdd", () => Edit(rowIndex, SequenceKeyframeCommand.Add, -1, frame));
        Item("editors.sequence.splitKey", "KeySplit", () => Edit(rowIndex, SequenceKeyframeCommand.Split, hit, frame), hit >= 0);
        Item("editors.sequence.deleteKey", "KeyDelete", () => Edit(rowIndex, SequenceKeyframeCommand.Delete, hit, 0), hit >= 0);
        menu.ShowAt(this, true);
    }
}
