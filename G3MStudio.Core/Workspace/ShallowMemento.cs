using System.Collections;
using System.Reflection;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>
/// The property values and list contents of some model objects, a few levels deep, kept so an import that changes them in place can be undone.
/// Objects are kept by reference; values are put back, lists are refilled.
/// </summary>
internal sealed class ShallowMemento
{
    private const int MaxObjects = 20000;

    private readonly List<(object Owner, PropertyInfo Property, object? Value)> _properties = [];
    private readonly List<(IList List, object?[] Items)> _lists = [];
    private readonly HashSet<object> _seen = new(ReferenceEqualityComparer.Instance);

    public void Capture(object? item, int depth)
    {
        if (item is null or string or GameMakerString || depth < 0 || _seen.Count >= MaxObjects || !_seen.Add(item)) return;
        // Arrays of numbers (audio and picture bytes) are replaced by new arrays, never changed in place; the property value restores them.
        if (item is Array { Length: > 0 } numbers && numbers.GetType().GetElementType()?.IsValueType == true) return;
        if (item is IList list)
        {
            var items = new object?[list.Count];
            list.CopyTo(items, 0);
            _lists.Add((list, items));
            foreach (var inner in items) Capture(inner, depth - 1);
            return;
        }

        if (item.GetType().Namespace?.StartsWith("G3MLib.DataFile.Models", StringComparison.Ordinal) != true) return;
        foreach (var property in item.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance).Where(p => p.GetIndexParameters().Length == 0 && p.GetMethod is not null && p.SetMethod?.IsPublic == true))
        {
            object? value;
            try { value = property.GetValue(item); }
            catch (TargetInvocationException) { continue; }
            _properties.Add((item, property, value));
            if (value is not null && !value.GetType().IsValueType) Capture(value, depth - 1);
        }
    }

    public void Restore()
    {
        foreach (var (owner, property, value) in _properties) property.SetValue(owner, value);
        foreach (var (list, items) in _lists)
        {
            // An array cannot be cleared or grown; its items are put back in place.
            if (list is Array array)
            {
                Array.Copy(items, array, items.Length);
                continue;
            }

            list.Clear();
            foreach (var item in items) list.Add(item);
        }
    }
}
