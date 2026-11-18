using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Why a resource without a found use may still be in use; the window shows the matching text.</summary>
public enum DeadReason { None, NameInCode, NamePrefixInCode, GlobalInit, DefaultFont, NumericRefs, Nickname, Tag, Favourite }

/// <param name="Type">Explorer type key.</param>
/// <param name="Name">Resource name.</param>
/// <param name="Reason">None when safe, otherwise why it may be used.</param>
/// <param name="Bytes">Estimated space in the data file (picture or audio data only); 0 when the type has no sizeable data.</param>
public sealed record DeadResource(string Type, string Name, DeadReason Reason, long Bytes)
{
    /// <summary>True when nothing suggests a use, so Delete all safe may remove it.</summary>
    public bool IsSafe => Reason == DeadReason.None;
}

/// <summary>
/// Resources that no other entry refers to, from the reference index. A resource counts as safe only when nothing hints at a dynamic use:
/// its name as a string in code (or a code string that is a prefix of the name; one or two characters only when code joins more text to them), a global init script, the default
/// font, a nickname, a tag or a favourite mark. Those are listed as maybe used. Uses held only by numbers cannot be seen by the index: before GameMaker 2023.8
/// code names every asset by its number, so in such a file no resource counts as safe.
/// shortcut: a resource used only by other dead resources is not found; delete the dead ones and scan again.
/// </summary>
public static class DeadResources
{
    private const int MinPrefix = 3;

    public static IReadOnlyList<DeadResource> Find(Workspace workspace, QuickAccess quick, CancellationToken cancellationToken = default)
    {
        using (workspace.Lock.Read())
        {
            var data = workspace.Data;
            var index = ReferenceCache.Of(data).Get(data, cancellationToken);
            var inits = new HashSet<GameMakerCode>(data.GlobalInitScripts.Select(i => i.Code).OfType<GameMakerCode>());
            var literals = new HashSet<string>(StringComparer.Ordinal);
            foreach (var text in data.Strings)
                if (text.Content is { Length: > 0 } content && index.FindUsages(text).Any(r => r.InstructionOffset >= 0)) literals.Add(content);
            var joined = JoinedShortLiterals(data, literals);
            // Older bytecode compiles sprite_index = spr_x or room_goto(rm_y) to plain integers, which the index cannot tell from other numbers.
            var numeric = !data.IsVersionAtLeast(2023, 8);
            var found = new List<DeadResource>();
            foreach (var type in ResourceTypes.All.Where(t => UsageSearch.AssetTypes.Contains(t.Key)))
            {
                var items = (type.Items(data) ?? []).ToList();
                foreach (var item in items)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var name = ResourceTypes.NameOf(item);
                    if (name.Length == 0 || index.FindUsages(item).Any(r => !UsageSearch.OwnedBy(r.Owner, item, type.Key, name))) continue;
                    if (item is GameMakerScript && index.FindFunctionUsages(UsageSearch.CodeNameOf(name)).Count > 0) continue;
                    var key = ResourceTypes.KeyOf(type.Key, name);
                    var reason =
                        literals.Contains(name) ? DeadReason.NameInCode
                        : HasLiteralPrefix(literals, joined, name) ? DeadReason.NamePrefixInCode
                        : item is GameMakerScript { Code: { } code } && inits.Contains(code.ParentEntry ?? code) ? DeadReason.GlobalInit
                        : type.Key == "fonts" && ReferenceEquals(item, items[0]) ? DeadReason.DefaultFont
                        : numeric ? DeadReason.NumericRefs
                        : workspace.Nicknames.Of(key) is not null ? DeadReason.Nickname
                        : quick.TagsOf(key).Count > 0 ? DeadReason.Tag
                        : quick.IsFavourite(key) ? DeadReason.Favourite
                        : DeadReason.None;
                    found.Add(new DeadResource(type.Key, name, reason, SizeOf(item)));
                }
            }

            return found;
        }
    }

    // A literal of one or two characters ("s", "s_") starts too many names to mean anything alone; it counts only when code joins more text to it.
    private static bool HasLiteralPrefix(HashSet<string> literals, HashSet<string> joined, string name)
    {
        for (var length = 1; length < name.Length; length++)
            if ((length < MinPrefix ? joined : literals).Contains(name[..length])) return true;
        return false;
    }

    // The short literals that code uses as the left side of a string join ("s_" + name): the push of the literal, the pushes of the value after it, then an add that takes it.
    // shortcut: a value read through another instance or an array (its push also pops) is not followed and the literal does not count; follow those pushes if such a name is ever deleted in use.
    private static HashSet<string> JoinedShortLiterals(GameMakerData data, HashSet<string> literals)
    {
        var joined = new HashSet<string>(StringComparer.Ordinal);
        if (!literals.Any(l => l.Length < MinPrefix)) return joined;
        foreach (var code in data.Code ?? [])
        {
            var list = code.Instructions;
            for (var i = 0; i < list.Count; i++)
                if (list[i].ValueString?.Resource?.Content is { Length: < MinPrefix } text && literals.Contains(text) && JoinedOnTheLeft(list, i)) joined.Add(text);
        }

        return joined;
    }

    private static bool JoinedOnTheLeft(List<GameMakerInstruction> list, int at)
    {
        var above = 0; // values pushed on top of the literal
        for (var i = at + 1; i < list.Count; i++)
        {
            var instruction = list[i];
            switch (GameMakerInstruction.GetInstructionType(instruction.Kind))
            {
                case GameMakerInstruction.InstructionType.PushInstruction:
                    above++;
                    break;
                case GameMakerInstruction.InstructionType.DoubleTypeInstruction when instruction.Kind == GameMakerInstruction.Opcode.Conv:
                    break;
                case GameMakerInstruction.InstructionType.CallInstruction:
                    above -= instruction.ArgumentsCount;
                    if (above < 0) return false; // the literal is an argument
                    above++;
                    break;
                case GameMakerInstruction.InstructionType.DoubleTypeInstruction or GameMakerInstruction.InstructionType.ComparisonInstruction:
                    if (above <= 1) return above == 1 && instruction.Kind == GameMakerInstruction.Opcode.Add;
                    above--;
                    break;
                default:
                    return false;
            }
        }

        return false;
    }

    /// <summary>Uncompressed picture bytes of a sprite or font, or the embedded audio of a sound.</summary>
    private static long SizeOf(object item) => item switch
    {
        GameMakerSprite s => s.Textures.Sum(t => t.Texture is { } p ? (long)p.SourceWidth * p.SourceHeight * 4 : 0),
        GameMakerFont f => f.Texture is { } p ? (long)p.SourceWidth * p.SourceHeight * 4 : 0,
        GameMakerSound s => s.AudioFile?.Data?.Length ?? 0,
        _ => 0,
    };
}
