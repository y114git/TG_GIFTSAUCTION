using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

public sealed partial class GroupListCommand
{
    private void Add(GameMakerData data)
    {
        CheckFree(data, name);
        if (audio)
        {
            var groups = data.AudioGroups ?? throw new InvalidOperationException("The data file has no audio groups.");
            var group = new GameMakerAudioGroup { Name = data.Strings.MakeString(name, out _), Path = data.Strings.MakeString($"audiogroup{groups.Count}.dat", out _) };
            groups.Add(group);
            _added = group;
        }
        else
        {
            var groups = data.TextureGroupInfo ?? throw new InvalidOperationException("The data file has no texture groups.");
            var group = new GameMakerTextureGroupInfo
            {
                Name = data.Strings.MakeString(name, out _),
                Directory = data.Strings.MakeString("", out _),
                Extension = data.Strings.MakeString(".png", out _),
                TexturePages = [],
                Sprites = [],
                SpineSprites = [],
                Fonts = [],
                Tilesets = [],
            };
            groups.Add(group);
            _added = group;
        }
    }

    private void Retitle(GameMakerData data)
    {
        CheckFree(data, newName);
        if (audio)
        {
            var group = data.AudioGroups?.FirstOrDefault(g => g.Name.Content == name) ?? throw new InvalidOperationException($"Audio group '{name}' does not exist.");
            (_oldName, group.Name) = (group.Name, data.Strings.MakeString(newName, out _));
        }
        else
        {
            var group = data.TextureGroupInfo?.FirstOrDefault(g => g.Name.Content == name) ?? throw new InvalidOperationException($"Texture group '{name}' does not exist.");
            (_oldName, group.Name) = (group.Name, data.Strings.MakeString(newName, out _));
        }
    }

    public override void Revert(GameMakerData data)
    {
        if (operation == New)
        {
            if (_added is null) throw new InvalidOperationException("The edit was never applied.");
            if (_added is GameMakerAudioGroup audioGroup) data.AudioGroups?.Remove(audioGroup);
            else if (_added is GameMakerTextureGroupInfo textureGroup) data.TextureGroupInfo?.Remove(textureGroup);
        }
        else if (_oldName is not null)
        {
            if (audio && data.AudioGroups?.FirstOrDefault(g => g.Name.Content == newName) is { } a) a.Name = _oldName;
            else if (!audio && data.TextureGroupInfo?.FirstOrDefault(g => g.Name.Content == newName) is { } t) t.Name = _oldName;
        }
    }
}
