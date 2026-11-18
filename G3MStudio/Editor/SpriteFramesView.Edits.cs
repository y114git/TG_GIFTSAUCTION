using Avalonia.Controls.Primitives;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

public sealed partial class SpriteFramesView
{
    // What the Sprite menu does for the buttons of the editor: the same code runs, so the two cannot differ.
    /// <summary>False when the sprite has no frame to resize.</summary>
    public bool RequestResize()
    {
        if (_sprite.Textures.Count == 0) return false;
        ResizeRequested?.Invoke();
        return true;
    }

    public void TogglePlay() => Flip(_play);

    public void ToggleLoop() => Flip(_loop);

    public void ToggleOnion() => Flip(_onion);

    public void ToggleLockOrigin() => Flip(_lock);

    private static void Flip(ToggleButton button) => button.IsChecked = button.IsChecked != true;

    private string SpriteName => _sprite.Name.Content ?? "";

    /// <summary>Raised with the reason when the workspace refuses a frame edit (a frame limit, a bad group), so the editor can show it.</summary>
    public event Action<string>? EditRefused;

    // The new selection is in place before the edit runs (the refresh it causes reads it); a refused edit puts the old one back.
    private async Task<bool> EditSelecting(Action select, Func<bool, EditCommand> build, string name)
    {
        var (selected, anchor, frame, current) = (_selected.ToArray(), _anchor, SelectedFrame, Current);
        select();
        if (await EditAsync(build, name)) return true;
        _selected.Clear();
        _selected.UnionWith(selected);
        (_anchor, SelectedFrame, Current) = (anchor, frame, current);
        return false;
    }

    private async Task<bool> EditAsync(Func<bool, EditCommand> build, string name)
    {
        if (_workspace is null) return false;
        if (RiskyEdit is { } risky) return await risky(_workspace, build, name);
        try
        {
            _workspace.Edit(build(false));
            return true;
        }
        catch (InvalidOperationException e)
        {
            EditRefused?.Invoke(e.Message);
            return false;
        }
    }

    /// <summary>Adds an empty frame after the last selected one.</summary>
    public Task<bool> AddAsync()
    {
        var at = _selected.Count == 0 ? _sprite.Textures.Count - 1 : _selected.Max();
        if (at < 0) return Task.FromResult(false);
        var name = SpriteName;
        return EditSelecting(() =>
        {
            _selected.Clear();
            _selected.Add(at + 1);
            (_anchor, SelectedFrame) = (at + 1, at + 1);
        }, _ => FrameListCommand.AddBlank(_sprite, at), name);
    }

    /// <summary>Puts a copy of every selected frame after it.</summary>
    public Task<bool> DuplicateAsync()
    {
        var frames = SelectedFrames;
        if (frames.Count == 0) return Task.FromResult(false);
        var name = SpriteName;
        // The copy of frame i lands after the copies of the frames before it.
        var copies = frames.Select((f, n) => f + n + 1).ToList();
        return EditSelecting(() =>
        {
            _selected.Clear();
            _selected.UnionWith(copies);
            (_anchor, SelectedFrame) = (copies[^1], copies[^1]);
        }, _ => new GroupCommand([.. frames.OrderDescending().Select(f => new FrameListCommand(name, FrameListCommand.Duplicate, f))]), name);
    }

    /// <summary>Deletes every selected frame; the last frame of a sprite is a warned refusal.</summary>
    public async Task<bool> DeleteAsync()
    {
        var frames = SelectedFrames;
        if (frames.Count == 0) return false;
        var name = SpriteName;
        var keep = Math.Min(frames[0], Math.Max(0, _sprite.Textures.Count - frames.Count - 1));
        var done = await EditAsync(force => new GroupCommand([.. frames.OrderDescending().Select(f => new FrameListCommand(name, FrameListCommand.Remove, f, force))]), name);
        if (done)
        {
            _selected.Clear();
            _selected.Add(keep);
            (_anchor, SelectedFrame) = (keep, keep);
            Choose(keep);
        }

        return done;
    }

    /// <summary>Moves the selected frames in front of the frame that is at <paramref name="to"/> now (the frame count puts them last), keeping their order.</summary>
    public Task<bool> MoveSelectedAsync(int to)
    {
        var frames = SelectedFrames;
        var order = FrameReorderCommand.MoveTo(_sprite.Textures.Count, frames, to);
        if (frames.Count == 0 || order.Select((v, i) => v == i).All(same => same)) return Task.FromResult(false);
        var name = SpriteName;
        return EditSelecting(() =>
        {
            _selected.Clear();
            _selected.UnionWith(Enumerable.Range(0, order.Length).Where(n => frames.Contains(order[n])));
            (_anchor, SelectedFrame) = (_selected.Min(), _selected.Min());
            Current = SelectedFrame;
        }, _ => new FrameReorderCommand(name, order), name);
    }
}
