using System.Collections;
using System.Reflection;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

/// <summary>Finds every model object reachable from some roots by walking fields, so a pruned file can drop what nothing points at.</summary>
static class Walker
{
    private static readonly Dictionary<Type, FieldInfo[]> Fields = [];

    private static FieldInfo[] FieldsOf(Type type)
    {
        if (Fields.TryGetValue(type, out var known)) return known;
        var all = new List<FieldInfo>();
        for (var t = type; t is not null && t != typeof(object); t = t.BaseType)
            all.AddRange(t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(f => !f.FieldType.IsValueType && f.FieldType != typeof(string) && !typeof(Delegate).IsAssignableFrom(f.FieldType) && f.FieldType != typeof(byte[])));
        return Fields[type] = [.. all];
    }

    // Edges the walk must not follow: back links to the whole file and the texture groups, which list every sprite.
    private static bool Skip(object o) => o is GameMakerData or GameMakerTextureGroupInfo or Type or Stream or Delegate or GameMakerChunk or GameMakerReader or GameMakerWriter
        || o.GetType().Namespace?.StartsWith("G3MLib", StringComparison.Ordinal) != true && o is not IEnumerable;

    public static HashSet<object> Reach(IEnumerable<object> roots)
    {
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        var stack = new Stack<object>();
        foreach (var root in roots) { seen.Add(root); stack.Push(root); }
        while (stack.Count > 0)
        {
            var o = stack.Pop();
            void Add(object? child)
            {
                if (child is null || child is string || child is byte[] || child.GetType().IsValueType || Skip(child) || !seen.Add(child)) return;
                stack.Push(child);
            }

            if (o is IEnumerable items && o is not string)
            {
                if (o is not byte[]) foreach (var item in items) Add(item);
                if (o.GetType().Namespace?.StartsWith("System", StringComparison.Ordinal) == true) continue;
            }

            foreach (var f in FieldsOf(o.GetType())) Add(f.GetValue(o));
        }

        return seen;
    }
}
