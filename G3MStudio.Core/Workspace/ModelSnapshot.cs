using System.Collections;
using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The lists of a data file (assets, code, strings, texture pages, audio) and the counters that an import changes, kept as they were so an
/// edit that adds items through the library's import can take them out again. Items that already existed are not copied: only their places.
/// </summary>
internal sealed class ModelSnapshot
{
    private static readonly string[] ListNames =
    [
        "Sprites", "Backgrounds", "Sounds", "Paths", "Scripts", "Shaders", "Fonts", "Timelines", "GameObjects", "Rooms", "Extensions", "Sequences",
        "AnimationCurves", "ParticleSystems", "Code", "CodeLocals", "Strings", "Variables", "Functions", "TexturePageItems", "EmbeddedTextures",
        "EmbeddedImages", "EmbeddedAudio", "AudioGroups", "TextureGroupInfo", "GlobalInitScripts", "GameEndScripts",
    ];

    private readonly List<(IList List, object?[] Items)> _lists = [];
    private readonly GameMakerData _data;
    private readonly (uint Instance, uint Global, uint Locals, uint LastObj, uint LastTile) _counters;

    public ModelSnapshot(GameMakerData data)
    {
        _data = data;
        foreach (var name in ListNames)
            if (typeof(GameMakerData).GetProperty(name)?.GetValue(data) is IList list) Keep(list);
        foreach (var group in data.TextureGroupInfo ?? [])
            foreach (var property in group.GetType().GetProperties().Where(p => p.GetIndexParameters().Length == 0))
                if (property.GetValue(group) is IList list && property.PropertyType != typeof(string)) Keep(list);
        _counters = (data.VarCount1, data.VarCount2, data.MaxLocalVarCount, data.GeneralInfo?.LastObj ?? 0, data.GeneralInfo?.LastTile ?? 0);
    }

    private void Keep(IList list)
    {
        var items = new object?[list.Count];
        list.CopyTo(items, 0);
        _lists.Add((list, items));
    }

    public void Restore()
    {
        foreach (var (list, items) in _lists)
        {
            list.Clear();
            foreach (var item in items) list.Add(item);
        }

        (_data.VarCount1, _data.VarCount2, _data.MaxLocalVarCount) = (_counters.Instance, _counters.Global, _counters.Locals);
        if (_data.GeneralInfo is { } info) (info.LastObj, info.LastTile) = (_counters.LastObj, _counters.LastTile);
    }
}
