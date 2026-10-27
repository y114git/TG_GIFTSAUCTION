using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using G3MLib.DataFile.Models;
using G3MLib.Modding.Resources;

namespace G3MLib.DataFile.Project;

/// <summary>
/// Data-file content the modding resource export has no type for: strings, variables, functions and UI layers.
/// </summary>
/// <remarks>
/// Variables and functions are rebuilt from the code, so they are written for reading and ignored on import.
/// </remarks>
internal static class ProjectDataTypes
{
    internal static readonly (string Type, string Folder)[] Folders =
    [
        ("Strings", "strings"), ("Variables", "variables"), ("Functions", "functions"), ("UILayers", "uilayers")
    ];

    private const string ModelNamespace = "G3MLib.DataFile.Models.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        NewLine = "\n",
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    internal static bool Handles(string type) => Folders.Any(f => f.Type == type);

    internal static void Export(string type, GameMakerData data, string typeDirectory, HashSet<string> filter)
    {
        switch (type)
        {
            case "Strings":
                Write(Path.Join(typeDirectory, "strings.json"), new JsonArray([.. (data.Strings ?? []).Select(s => (JsonNode)JsonValue.Create(s?.Content))]));
                break;
            case "Variables":
                Write(Path.Join(typeDirectory, "variables.json"), new JsonArray([.. (data.Variables ?? []).Select(v => (JsonNode)new JsonObject
                {
                    ["name"] = v.Name?.Content,
                    ["instanceType"] = (int)v.InstanceType,
                    ["varID"] = v.VarID
                })]));
                break;
            case "Functions":
                Write(Path.Join(typeDirectory, "functions.json"), new JsonArray([.. (data.Functions ?? []).Select(f => (JsonNode)new JsonObject { ["name"] = f.Name?.Content })]));
                break;
            case "UILayers":
                ExportUILayers(data, typeDirectory, filter);
                break;
        }
    }

    internal static void Import(string type, GameMakerData data, string typeDirectory)
    {
        switch (type)
        {
            case "Strings":
                ImportStrings(data, Path.Join(typeDirectory, "strings.json"));
                break;
            case "UILayers":
                ImportUILayers(data, typeDirectory);
                break;
        }
    }

    // Changes strings in place by position and appends extra ones; shortening the list is not supported.
    private static void ImportStrings(GameMakerData data, string file)
    {
        if (!File.Exists(file))
        {
            return;
        }
        JsonArray strings = JsonNode.Parse(File.ReadAllText(file)).AsArray();
        for (int i = 0; i < strings.Count; i++)
        {
            string content = strings[i]?.GetValue<string>();
            if (i < data.Strings.Count)
            {
                if (data.Strings[i].Content != content)
                {
                    data.Strings[i].Content = content;
                }
            }
            else if (data.Strings is GameMakerObservableList<GameMakerString> list)
            {
                list.InternalAdd(new GameMakerString(content));
            }
            else
            {
                data.Strings.Add(new GameMakerString(content));
            }
        }
    }

    private static void ExportUILayers(GameMakerData data, string typeDirectory, HashSet<string> filter)
    {
        IList<GameMakerUIRootNode> roots = data.FORM.UILR?.List;
        if (roots is null)
        {
            return;
        }
        for (int i = 0; i < roots.Count; i++)
        {
            string name = roots[i]?.Name?.Content;
            if (name is null || (filter is not null && !filter.Contains(name)))
            {
                continue;
            }
            string directory = Path.Join(typeDirectory, ResourceExportService.SafeName(name));
            Directory.CreateDirectory(directory);
            Write(Path.Join(directory, "uilayer.json"), ToJson(roots[i], typeof(GameMakerUIRootNode)));
        }
    }

    private static void ImportUILayers(GameMakerData data, string typeDirectory)
    {
        GameMakerChunkUILR chunk = data.FORM.UILR;
        if (chunk is null)
        {
            return;
        }
        chunk.List ??= new();
        ResourceLookup lookup = new(data);
        foreach (string directory in Directory.EnumerateDirectories(typeDirectory).Order(StringComparer.Ordinal))
        {
            string file = Path.Join(directory, "uilayer.json");
            if (!File.Exists(file))
            {
                continue;
            }
            GameMakerUIRootNode root = (GameMakerUIRootNode)FromJson(JsonNode.Parse(File.ReadAllText(file)), typeof(GameMakerUIRootNode), lookup);
            int existing = chunk.List.ToList().FindIndex(r => r?.Name?.Content == root.Name?.Content);
            if (existing >= 0)
            {
                chunk.List[existing] = root;
            }
            else
            {
                chunk.List.Add(root);
            }
        }
    }

    private static void Write(string path, JsonNode node)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, node.ToJsonString(JsonOptions));
    }

    private static bool IsPlain(Type type) => type.IsPrimitive || type.IsEnum || type == typeof(string);

    private static IEnumerable<PropertyInfo> DataProperties(Type type)
    {
        return type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0 && p.CanRead && p.CanWrite && p.GetSetMethod() is not null
                && !(type == typeof(GameMakerUIRootNode) && p.Name == "Name"))
            .OrderBy(p => p.Name, StringComparer.Ordinal);
    }

    private static JsonNode ToJson(object value, Type declared)
    {
        switch (value)
        {
            case null:
                return null;
            case GameMakerString s:
                return JsonValue.Create(s.Content);
            case GameMakerFlexValue flex:
                return new JsonObject { ["value"] = flex.Value, ["unit"] = (int)flex.Unit };
            case Enum e:
                return JsonValue.Create(Convert.ToInt64(e));
            case string text:
                return JsonValue.Create(text);
        }
        Type type = value.GetType();
        if (type.IsPrimitive)
        {
            return JsonSerializer.SerializeToNode(value, type);
        }
        if (value is GameMakerNamedResource named && declared != typeof(GameMakerUIRootNode) && !typeof(IGameMakerUINodeData).IsAssignableFrom(type))
        {
            return JsonValue.Create(named.Name?.Content);
        }
        if (value is IList list)
        {
            return new JsonArray([.. list.Cast<object>().Select(item => ToJson(item, item?.GetType()))]);
        }
        JsonObject result = new();
        if (type != declared)
        {
            result["$type"] = type.FullName[ModelNamespace.Length..];
        }
        foreach (PropertyInfo property in DataProperties(type))
        {
            result[property.Name] = ToJson(property.GetValue(value), property.PropertyType);
        }
        return result;
    }

    private static object FromJson(JsonNode node, Type declared, ResourceLookup lookup)
    {
        if (declared == typeof(GameMakerString))
        {
            return node is null ? null : lookup.Data.Strings.MakeString(node.GetValue<string>());
        }
        if (declared == typeof(GameMakerFlexValue))
        {
            return new GameMakerFlexValue(node["value"].GetValue<float>(), (GameMakerFlexValue.UnitKind)node["unit"].GetValue<int>());
        }
        if (declared.IsEnum)
        {
            return Enum.ToObject(declared, node.GetValue<long>());
        }
        if (IsPlain(declared))
        {
            return node is null ? null : node.Deserialize(declared);
        }
        if (node is null)
        {
            return null;
        }
        if (node is JsonValue && typeof(GameMakerNamedResource).IsAssignableFrom(declared) && lookup.Find(declared, node.GetValue<string>()) is { } resource)
        {
            return resource;
        }
        if (node is JsonArray array)
        {
            IList list = (IList)Activator.CreateInstance(declared);
            Type element = declared.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>)).GetGenericArguments()[0];
            foreach (JsonNode item in array)
            {
                list.Add(FromJson(item, element, lookup));
            }
            return list;
        }
        Type type = node["$type"] is { } typeName ? declared.Assembly.GetType(ModelNamespace + typeName.GetValue<string>(), true) : declared;
        object instance = Activator.CreateInstance(type);
        foreach (PropertyInfo property in DataProperties(type))
        {
            if (node.AsObject().TryGetPropertyValue(property.Name, out JsonNode child))
            {
                property.SetValue(instance, FromJson(child, property.PropertyType, lookup));
            }
        }
        return instance;
    }

    // Finds a named resource of a given model type in the data file's resource lists.
    private sealed class ResourceLookup
    {
        private readonly Dictionary<Type, IList> _lists = [];

        internal ResourceLookup(GameMakerData data)
        {
            Data = data;
            foreach (PropertyInfo property in typeof(GameMakerData).GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                Type list = property.PropertyType.GetInterfaces().Append(property.PropertyType)
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IList<>));
                if (list is not null && typeof(GameMakerNamedResource).IsAssignableFrom(list.GetGenericArguments()[0])
                    && property.GetIndexParameters().Length == 0 && property.GetValue(data) is IList values)
                {
                    _lists.TryAdd(list.GetGenericArguments()[0], values);
                }
            }
        }

        internal GameMakerData Data { get; }

        internal object Find(Type type, string name)
        {
            return _lists.TryGetValue(type, out IList list)
                ? list.Cast<GameMakerNamedResource>().FirstOrDefault(r => r?.Name?.Content == name)
                : null;
        }
    }
}
