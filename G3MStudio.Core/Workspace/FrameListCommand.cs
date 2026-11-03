using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

#pragma warning disable CA1001 // the entry belongs to the sprite or is put back into it; the model disposes it
/// <summary>Adds a copy of a sprite frame after it, or removes a frame. A copy shares the picture of the frame it came from. The last frame cannot be removed.</summary>
public sealed class FrameListCommand(string sprite, string operation, int frame) : EditCommand
{
    public const string Name = "frameList";
    public const string Duplicate = "duplicate";
    public const string Remove = "remove";

    private GameMakerSprite.TextureEntry? _entry;

    public override string Kind => Name;

    public override string Editor => "sprites";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string>
    {
        ["sprite"] = sprite,
        ["operation"] = operation,
        ["frame"] = frame.ToString(System.Globalization.CultureInfo.InvariantCulture),
    };

    private GameMakerSprite Find(GameMakerData data) => data.Sprites.FirstOrDefault(s => s.Name.Content == sprite) ?? throw new InvalidOperationException($"Sprite '{sprite}' does not exist.");

    public override void Apply(GameMakerData data)
    {
        var target = Find(data);
        if (frame < 0 || frame >= target.Textures.Count) throw new InvalidOperationException($"Sprite '{sprite}' has no frame {frame}.");
        switch (operation)
        {
            case Duplicate:
                _entry = new GameMakerSprite.TextureEntry { Texture = target.Textures[frame].Texture };
                target.Textures.Insert(frame + 1, _entry);
                break;
            case Remove:
                if (target.Textures.Count < 2) throw new InvalidOperationException("The last frame of a sprite cannot be removed.");
                _entry = target.Textures[frame];
                target.Textures.RemoveAt(frame);
                break;
            default:
                throw new InvalidDataException($"Unknown frame operation '{operation}'.");
        }
    }

    public override void Revert(GameMakerData data)
    {
        var target = Find(data);
        if (operation == Duplicate) target.Textures.RemoveAt(frame + 1);
        else target.Textures.Insert(frame, _entry ?? throw new InvalidOperationException("The frame was never removed."));
    }
}
#pragma warning restore CA1001
