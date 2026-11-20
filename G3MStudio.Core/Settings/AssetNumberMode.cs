namespace G3MStudio.Core.Settings;

/// <summary>Off: no replacement offered. Manual: the menu and the commands offer it. Auto: an entry that opens has its asset numbers written as names.</summary>
public enum AssetNumberMode { Off, Manual, Auto }

/// <summary>The asset types a number in code can be replaced for (Explorer type keys).</summary>
public static class AssetNumberTypes
{
    public static readonly IReadOnlyList<string> All = ["sprites", "objects", "rooms", "sounds", "paths", "fonts", "shaders", "scripts", "tileSets", "particleSystems"];
}
