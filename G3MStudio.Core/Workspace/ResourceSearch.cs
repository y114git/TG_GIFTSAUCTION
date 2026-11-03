using G3MLib.DataFile;

namespace G3MStudio.Core.Workspace;

public sealed record QuickOpenItem(string Type, string Name)
{
    public string Key => ResourceTypes.KeyOf(Type, Name);
}

/// <summary>Go to Resource (design/shell.md 3.3): names across the resources and code entries, a type prefix such as <c>spr:</c> narrows it.</summary>
public static class ResourceSearch
{
    /// <summary>
    /// Best matches first: exact, name start, word initials, then any substring; ties keep the recent order, then the list order.
    /// With no text the recent keys are listed. Without a prefix only resource types and code are searched; a prefix selects any type.
    /// </summary>
    public static IReadOnlyList<QuickOpenItem> Search(GameMakerData data, string query, IReadOnlyList<string> recent, int limit = 50)
    {
        var (typeKey, needle) = ExplorerModel.SplitPrefix(query.Trim());
        if (needle.Length == 0 && typeKey.Length == 0)
            return [.. recent.Where(k => ResourceTypes.Find(data, k) is not null).Select(k => ResourceTypes.SplitKey(k)!.Value).Select(k => new QuickOpenItem(k.Type, k.Name)).Take(limit)];

        var order = new Dictionary<string, int>();
        for (var i = 0; i < recent.Count; i++) order.TryAdd(recent[i], i);
        var types = ResourceTypes.All.Where(t => typeKey.Length > 0 ? t.Key == typeKey : !t.IsData || t.Key == "code");
        return [.. types
            .SelectMany(t => (t.Items(data) ?? []).Select(i => new QuickOpenItem(t.Key, ResourceTypes.NameOf(i))))
            .Select(i => (Item: i, Rank: Rank(i.Name, needle)))
            .Where(x => x.Rank < 4)
            .OrderBy(x => x.Rank)
            .ThenBy(x => order.GetValueOrDefault(x.Item.Key, int.MaxValue))
            .Take(limit)
            .Select(x => x.Item)];
    }

    private static int Rank(string name, string needle)
    {
        if (needle.Length == 0) return 3;
        if (name.Equals(needle, StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.StartsWith(needle, StringComparison.OrdinalIgnoreCase)) return 1;
        if (Initials(name).StartsWith(needle, StringComparison.OrdinalIgnoreCase)) return 2;
        return name.Contains(needle, StringComparison.OrdinalIgnoreCase) ? 3 : 4;
    }

    // "obj_player_hit" gives "oph"; "PlayerHit" gives "ph".
    private static string Initials(string name)
    {
        var letters = new List<char>();
        for (var i = 0; i < name.Length; i++)
            if (name[i] != '_' && (i == 0 || name[i - 1] == '_' || char.IsUpper(name[i]) && !char.IsUpper(name[i - 1]))) letters.Add(name[i]);
        return new string([.. letters]);
    }
}
