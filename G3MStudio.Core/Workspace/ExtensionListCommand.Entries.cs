using System.Collections;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

public sealed partial class ExtensionListCommand
{
    private IList ListOf(GameMakerData data, out object root)
    {
        root = PropertyModel.FindRoot(data, "extensions", extension) ?? throw new InvalidOperationException($"Extension '{extension}' does not exist.");
        return PropertyModel.ReadRaw(root, list) as IList ?? throw new InvalidOperationException($"'{list}' is not a list.");
    }

    public override void Revert(GameMakerData data)
    {
        var items = ListOf(data, out _);
        switch (operation)
        {
            case Add:
                items.RemoveAt(_at);
                break;
            case Remove:
                items.Insert(Math.Clamp(_at, 0, items.Count), _removed);
                break;
            case Move:
                items.RemoveAt(to);
                items.Insert(_at, _removed);
                break;
        }
    }

    private void Check(IList items, int at)
    {
        if (at < 0 || at >= items.Count) throw new InvalidOperationException($"'{list}' has no entry {at}.");
    }

    /// <summary>The GML names a removed entry takes out: the function itself, or every function of a file.</summary>
    private static IEnumerable<string> CalledNames(object? entry) => entry switch
    {
        GameMakerExtensionFunction f => f.Name?.Content is { Length: > 0 } n ? [n] : [],
        GameMakerExtensionFile file => file.Functions.SelectMany(CalledNames),
        _ => [],
    };

    private static bool UsedInCode(GameMakerData data, string name)
    {
        var function = data.Functions?.FirstOrDefault(f => f.Name?.Content == name);
        return function is not null && ReferenceCache.Of(data).Get(data).FindUsages(function).Any(u => u.InstructionOffset >= 0);
    }

    // A new entry: empty strings from the table and the first valid value of every type field, so the entry can be written to the file as it is.
    private static object NewEntry(GameMakerData data, IList items, object root, string fileKind)
    {
        var type = items.GetType().GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)).GetGenericArguments()[0];
        var entry = Activator.CreateInstance(type) ?? throw new InvalidOperationException($"Cannot create an entry of type {type.Name}.");
        foreach (var property in type.GetProperties().Where(p => p.PropertyType == typeof(GameMakerString) && p.CanWrite)) property.SetValue(entry, data.Strings.MakeString("", out _));
        switch (entry)
        {
            case GameMakerExtensionFile file:
                // The template of a new file: its kind (DLL, GML, JS, ...); anything unknown is a DLL.
                file.Kind = Enum.TryParse<GameMakerExtensionKind>(fileKind, out var kind) && Enum.IsDefined(kind) ? kind : GameMakerExtensionKind.Dll;
                break;
            case GameMakerExtensionFunction function:
                function.RetType = GameMakerExtensionVarType.Double;
                function.Kind = items.OfType<GameMakerExtensionFunction>().LastOrDefault()?.Kind ?? StdcallKind;
                function.ID = ((GameMakerExtension)root).Files.SelectMany(f => f.Functions).Select(f => f.ID).DefaultIfEmpty().Max() + 1;
                break;
        }

        return entry;
    }
}
