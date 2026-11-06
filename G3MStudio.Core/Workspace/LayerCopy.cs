using System.Reflection;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using static G3MLib.DataFile.Models.GameMakerRoom;

namespace G3MStudio.Core.Workspace;

/// <summary>Copies a room layer and what it holds, for Duplicate Layer. Public read/write properties are copied by value, lists and tile rows are copied so the two layers share nothing editable.</summary>
internal static class LayerCopy
{
    public static T Shallow<T>(T source) where T : class, new() => (T)CloneObject(source);

    /// <summary>A layer's effect parameters copied into a list of their own (two layers must not share one list).</summary>
    public static GameMakerSimpleList<EffectProperty> Properties(GameMakerSimpleList<EffectProperty>? source) => Items(source);

    /// <summary>Copies the data of a layer. Instances are added to the room's master list with new ids and also returned in <paramref name="created"/>.</summary>
    public static Layer.LayerData Data(Layer.LayerData? source, Layer owner, GameMakerRoom room, GameMakerData data, List<GameObject> created)
    {
        switch (source)
        {
            case Layer.LayerInstancesData instances:
                var next = Math.Max(data.GeneralInfo.LastObj, data.Rooms.SelectMany(RoomInstanceCommand.Lists).SelectMany(l => l).Select(i => i.InstanceID).DefaultIfEmpty().Max());
                var copy = new Layer.LayerInstancesData();
                foreach (var instance in instances.Instances)
                {
                    var clone = Shallow(instance);
                    clone.InstanceID = ++next;
                    copy.Instances.Add(clone);
                    room.GameObjects?.Add(clone);
                    created.Add(clone);
                }

                data.GeneralInfo.LastObj = next;
                return copy;
            case Layer.LayerTilesData tiles:
                var tileCopy = Shallow(tiles);
                tileCopy.ParentLayer = owner;
                tileCopy.TileData = [.. tiles.TileData.Select(r => (uint[])r.Clone())];
                return tileCopy;
            case Layer.LayerBackgroundData background:
                var backgroundCopy = Shallow(background);
                backgroundCopy.ParentLayer = owner;
                return backgroundCopy;
            case Layer.LayerAssetsData assets:
                return AssetsCopy(assets);
            case Layer.LayerEffectData effect:
                return new Layer.LayerEffectData { EffectType = effect.EffectType, Properties = owner.EffectProperties };
            default:
                throw new InvalidOperationException("This layer type has no data to copy.");
        }
    }

    private static Layer.LayerAssetsData AssetsCopy(Layer.LayerAssetsData assets) => new()
    {
        LegacyTiles = Items(assets.LegacyTiles),
        Sprites = Items(assets.Sprites),
        Sequences = Items(assets.Sequences),
        ParticleSystems = Items(assets.ParticleSystems),
        TextItems = Items(assets.TextItems),
    };

    private static T Items<T>(T? list) where T : class, System.Collections.IList, new()
    {
        var copy = new T();
        foreach (var item in list ?? (System.Collections.IList)copy) copy.Add(CloneObject(item));
        return copy;
    }

    private static object CloneObject(object item)
    {
        var copy = Activator.CreateInstance(item.GetType()) ?? throw new InvalidOperationException("The element type has no constructor.");
        foreach (var property in item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance))
            if (property is { CanRead: true, CanWrite: true } && property.GetIndexParameters().Length == 0) property.SetValue(copy, property.GetValue(item));
        return copy;
    }
}
