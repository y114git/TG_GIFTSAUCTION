using System.Text.Json;
using System.Text.Json.Serialization;

namespace G3MStudio.Core.Plugins;

/// <summary>plugin.json of a plugin folder (ADR-0009). Only id, name, version and entry are required.</summary>
public sealed record PluginManifest
{
    public string Id { get; init; } = "";

    public string Name { get; init; } = "";

    public string Version { get; init; } = "";

    public string Description { get; init; } = "";

    /// <summary>The source file that holds the plugin class; the other .cs files under src are compiled with it.</summary>
    public string Entry { get; init; } = "src/Main.cs";

    public string[] Authors { get; init; } = [];

    public string[] Permissions { get; init; } = [];

    public string Homepage { get; init; } = "";

    [JsonIgnore]
    public bool IsValid => Id.Length > 0 && Name.Length > 0 && Version.Length > 0;

    public static PluginManifest Parse(string json) => JsonSerializer.Deserialize<PluginManifest>(json, Options) ?? throw new InvalidDataException("plugin.json is empty.");

    public static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true, WriteIndented = true };
}
