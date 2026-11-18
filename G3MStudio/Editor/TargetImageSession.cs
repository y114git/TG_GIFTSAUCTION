using System.Runtime.CompilerServices;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

/// <summary>
/// A picture target (a page item or a texture page) edited in a tab: edits stay local with their own undo, and Apply writes the picture through the target's
/// undoable command. <see cref="Dirty"/> is true while the picture differs from what was last applied, so a tab can show it and never drops it unasked.
/// </summary>
public sealed class TargetImageSession : LocalImageSession
{
    private PixelBuffer _applied;
    private (int X, int Y) _appliedAt;

    // Where the top left of the picture read at the start lies in each picture of the undo list (null after a scale, rotation or skew); a font sheet moves its glyphs by it.
    private readonly ConditionalWeakTable<PixelBuffer, StrongBox<(int X, int Y)?>> _corners = [];

    public TargetImageSession(IPictureTarget target, PixelBuffer picture) : base(picture)
    {
        Target = target;
        _applied = Current;
    }

    public IPictureTarget Target { get; }

    /// <summary>The picture the target held when this editor last read or wrote it; if the target holds another one at Apply, it changed elsewhere.</summary>
    public PixelBuffer? Baseline { get; set; }

    /// <summary>Works out where a picture of the given size goes on the texture pages (the window reads the data file under its lock); null when the target has no page item.</summary>
    public Func<int, int, FitOptions?, IReadOnlyList<FitStep>?>? Planner { get; set; }

    /// <summary>The texture pages the Advanced choices of the Resize dialog offer, and the largest page side without a choice.</summary>
    public IReadOnlyList<string> Pages { get; set; } = [];

    public int AutomaticMax { get; set; }

    /// <summary>The Advanced choices of the last Resize or canvas drag; Apply places the picture with them (the remembered ones when there was none).</summary>
    public FitOptions? Fit { get; private set; }

    private (int X, int Y)? CornerOf(PixelBuffer picture) => _corners.TryGetValue(picture, out var box) ? box.Value : (0, 0);

    /// <summary>How far the picture last applied moved within the current one by canvas changes since then; null after a scale, rotation or skew.</summary>
    public (int X, int Y)? Shift => CornerOf(Current) is { } at ? (at.X - _appliedAt.X, at.Y - _appliedAt.Y) : null;

    public override ResizeContext? ResizeContext => Planner is null ? null : new ResizeContext(1, [], null, Pages, AutomaticMax);

    public override IReadOnlyList<FitStep>? PlanResize(int frame, ResizeSpec spec, bool allFrames, FitOptions? fit = null) => Planner?.Invoke(spec.Width, spec.Height, fit);

    public override bool CommitResize(int frame, ResizeSpec spec, bool allFrames, string? group, FitOptions? fit = null)
    {
        var before = Current;
        Fit = fit;
        Replace(ImageEditCommand.Run(before, null, [spec.Op]));
        _corners.AddOrUpdate(Current, new StrongBox<(int X, int Y)?>(FontSheet.Follow(CornerOf(before), before, null, [spec.Op])));
        RaiseDirty();
        return true;
    }

    /// <summary>True when the target keeps one size (a texture page, a mask, a glyph): the canvas handles are off.</summary>
    public bool FixedSize { get; set; }

    /// <summary>The limit of the target, worded by the window; the status line shows it.</summary>
    public string? Note { get; set; }

    public bool Dirty => !ReferenceEquals(Current, _applied);

    public override bool Pending => Dirty;

    /// <summary>Drops the edits: the editor goes back to the picture last applied (one more step of its own undo).</summary>
    public override bool Discard()
    {
        if (!Dirty) return true;
        ReplaceWith(_applied.Copy());
        MarkApplied();
        return true;
    }

    public event Action? DirtyChanged;

    private void RaiseDirty()
    {
        DirtyChanged?.Invoke();
        OnPendingChanged();
    }

    public override void Commit(int frame, ImageEdit edit)
    {
        var before = Current;
        base.Commit(frame, edit);
        _corners.AddOrUpdate(Current, new StrongBox<(int X, int Y)?>(FontSheet.Follow(CornerOf(before), before, edit.Selection, edit.Ops)));
        RaiseDirty();
    }

    public override void Undo()
    {
        base.Undo();
        RaiseDirty();
    }

    public override void Redo()
    {
        base.Redo();
        RaiseDirty();
    }

    /// <summary>Puts a whole new picture in the editor as one step of its own undo; the limit of the target is checked when it is applied.</summary>
    public void ReplaceWith(PixelBuffer picture)
    {
        var corner = CornerOf(Current);
        Replace(picture);
        _corners.AddOrUpdate(Current, new StrongBox<(int X, int Y)?>(corner));
        RaiseDirty();
    }

    public override void Accept() => MarkApplied();

    public void MarkApplied()
    {
        Fit = null; // the page choice of an earlier dialog is spent
        _applied = Current;
        // After a scale, rotation or skew the corner is unknown; the applied picture is then the reference later canvas changes are measured from.
        if (CornerOf(Current) is null) _corners.AddOrUpdate(Current, new StrongBox<(int X, int Y)?>((0, 0)));
        _appliedAt = CornerOf(Current) ?? (0, 0);
        RaiseDirty();
    }
}
