using Avalonia;
using Avalonia.Input;

namespace G3MStudio.Editor;

internal sealed partial class CanvasHandles
{
    public bool Begin(PointerPressedEventArgs e, Point at)
    {
        if (HandleAt(at) is not { } side) return false;
        (_side, _from, _at, _to, _pointer, _stretch, _modAll) = (side, at, at, size(), e.Pointer, e.KeyModifiers.HasFlag(KeyModifiers.Control), e.KeyModifiers.HasFlag(ResizeScope.Modifier));
        e.Pointer.Capture(host);
        DragChanged?.Invoke(Drag);
        host.InvalidateVisual();
        return true;
    }

    /// <summary>Follows the pointer; true while a drag is on. Without a drag it only tracks the hover.</summary>
    public bool Move(Point at, KeyModifiers modifiers)
    {
        if (_side is not { } side)
        {
            SetHover(HandleAt(at));
            return false;
        }

        _at = at;
        Apply(side, modifiers);
        return true;
    }

    /// <summary>A modifier key went down or up without the pointer moving: Ctrl (stretch), Shift (aspect) and the all-frames key take effect at once. False when no drag is on.</summary>
    public bool Modifiers(KeyModifiers modifiers)
    {
        if (_side is not { } side) return false;
        Apply(side, modifiers);
        return true;
    }

    private void Apply((int X, int Y) side, KeyModifiers modifiers)
    {
        var stretch = modifiers.HasFlag(KeyModifiers.Control);
        var next = DragSize(size(), side, (_at - _from) / zoom(), modifiers.HasFlag(KeyModifiers.Shift));
        var all = modifiers.HasFlag(ResizeScope.Modifier);
        if (next != _to || stretch != _stretch || all != _modAll)
        {
            (_to, _stretch, _modAll) = (next, stretch, all);
            DragChanged?.Invoke(Drag);
        }

        host.InvalidateVisual();
    }

    /// <summary>The button was released (with the modifier keys held at that moment): reports the size once through <see cref="Dropped"/>.</summary>
    public bool End(KeyModifiers? modifiers = null)
    {
        if (modifiers is { } held) Modifiers(held);
        if (Drag is not { } spec) return false;
        var pointer = _pointer;
        (_side, _pointer) = (null, null);
        pointer?.Capture(null); // no drag is on by now, so losing the capture does not cancel anything
        DragChanged?.Invoke(null);
        host.InvalidateVisual();
        if (size() == (spec.Width, spec.Height)) return true;
        Dropped?.Invoke(spec);
        return true;
    }

    private void SetHover((int X, int Y)? hover)
    {
        if (hover == _hover) return;
        _hover = hover;
        SideChanged?.Invoke();
        Hovered?.Invoke(hover is not null);
    }

    public void ClearHover()
    {
        if (_side is null) SetHover(null);
    }

    /// <summary>Escape: the drag ends and the canvas keeps its size.</summary>
    public bool Cancel()
    {
        if (_side is null) return false;
        _side = null;
        _pointer?.Capture(null);
        _pointer = null;
        DragChanged?.Invoke(null);
        host.InvalidateVisual();
        return true;
    }
}
