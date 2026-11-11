using G3MStudio.Core.Workspace;

namespace G3MStudio.Editor;

public sealed partial class SpriteFramesView
{
    private string SpriteName => _sprite.Name.Content ?? "";

    private async Task<bool> EditAsync(Func<bool, EditCommand> build, string name)
    {
        if (_workspace is null) return false;
        if (RiskyEdit is { } risky) return await risky(_workspace, build, name);
        try
        {
            _workspace.Edit(build(false));
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Adds an empty frame after the last selected one.</summary>
    public Task<bool> AddAsync()
    {
        var at = _selected.Count == 0 ? _sprite.Textures.Count - 1 : _selected.Max();
        if (at < 0) return Task.FromResult(false);
        var blank = FramePicture.ToPng(new PixelBuffer((int)Math.Max(1, _sprite.Width), (int)Math.Max(1, _sprite.Height)));
        var name = SpriteName;
        _selected.Clear();
        _selected.Add(at + 1);
        (_anchor, SelectedFrame) = (at + 1, at + 1);
        return EditAsync(_ => new GroupCommand([new FrameListCommand(name, FrameListCommand.Duplicate, at), new FrameOwnPictureCommand(name, at + 1, blank)]), name);
    }

    /// <summary>Puts a copy of every selected frame after it.</summary>
    public Task<bool> DuplicateAsync()
    {
        var frames = SelectedFrames;
        if (frames.Count == 0) return Task.FromResult(false);
        var name = SpriteName;
        // The copy of frame i lands after the copies of the frames before it.
        var copies = frames.Select((f, n) => f + n + 1).ToList();
        _selected.Clear();
        _selected.UnionWith(copies);
        (_anchor, SelectedFrame) = (copies[^1], copies[^1]);
        return EditAsync(_ => new GroupCommand([.. frames.OrderDescending().Select(f => new FrameListCommand(name, FrameListCommand.Duplicate, f))]), name);
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
        _selected.Clear();
        _selected.UnionWith(Enumerable.Range(0, order.Length).Where(n => frames.Contains(order[n])));
        (_anchor, SelectedFrame) = (_selected.Min(), _selected.Min());
        Current = SelectedFrame;
        return EditAsync(_ => new FrameReorderCommand(name, order), name);
    }
}
