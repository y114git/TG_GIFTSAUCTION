using System.IO.Compression;
using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace G3MStudio.Core.Plugins;

public enum PluginState
{
    Active,
    Disabled,
    Failed,
}

/// <summary>One plugin folder: what its manifest says, whether it runs, and what it wrote to its log.</summary>
public sealed class PluginInfo(string folder, PluginManifest manifest)
{
    public string Folder { get; } = folder;

    public PluginManifest Manifest { get; internal set; } = manifest;

    public PluginState State { get; internal set; } = PluginState.Disabled;

    public string? Error { get; internal set; }

    public List<string> Log { get; } = [];

    public List<string> CommandIds { get; } = [];

    internal List<(string TypeKey, Func<object, object?> Create)> Editors { get; } = [];

    /// <summary>The GitHub address the plugin was installed from, or null. Installing from it again updates the plugin.</summary>
    public string? Origin => File.Exists(OriginFile) ? File.ReadLines(OriginFile).FirstOrDefault()?.Trim() : null;

    /// <summary>The commit the plugin was installed at (from the name of the folder inside a GitHub ZIP), or null.</summary>
    public string? Revision => File.Exists(OriginFile) ? File.ReadLines(OriginFile).Skip(1).FirstOrDefault()?.Trim() : null;

    internal string OriginFile => Path.Combine(Folder, ".origin");

    internal IStudioPlugin? Instance { get; set; }

    internal AssemblyLoadContext? Context { get; set; }
}

/// <summary>
/// Finds plugin folders under the user data folder, compiles their C# sources with Roslyn, loads each into its own collectible load context and
/// runs it. A plugin that fails to compile or start is turned off with its error; nothing a plugin does can stop the host (ADR-0009).
/// </summary>
public sealed class PluginHost
{
    public const string ManifestName = "plugin.json";

    private readonly UserData _userData;
    private readonly CommandRegistry _registry;
    private readonly Func<Workspace.Workspace?> _workspace;
    private readonly Action<NotificationLevel, string> _notify;
    private readonly List<PluginInfo> _plugins = [];
    private static readonly Lazy<IReadOnlyList<MetadataReference>> HostReferences = new(() =>
        [.. ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "").Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries).Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))]);

    public PluginHost(UserData userData, CommandRegistry registry, Func<Workspace.Workspace?> workspace, Action<NotificationLevel, string> notify)
        => (_userData, _registry, _workspace, _notify) = (userData, registry, workspace, notify);

    public string Folder => _userData.Combine("plugins");

    public IReadOnlyList<PluginInfo> Plugins => _plugins;

    /// <summary>The view the first active plugin editor for this type builds for the resource, or null. An editor that throws is logged and skipped.</summary>
    public object? CreateEditorView(string typeKey, object? resource)
    {
        if (resource is null) return null;
        foreach (var plugin in _plugins.Where(p => p.State == PluginState.Active))
            foreach (var (key, create) in plugin.Editors.Where(e => e.TypeKey == typeKey))
            {
                try
                {
                    if (create(resource) is { } view) return view;
                }
                catch (Exception e)
                {
                    plugin.Log.Add(e.ToString());
                    _notify(NotificationLevel.Error, $"'{plugin.Manifest.Name}': {e.Message}");
                }
            }

        return null;
    }

    public event Action? Changed;

    /// <summary>A plugin panel was opened from its command: the window should show the content.</summary>
    public event Action<string, Func<object>>? PanelRequested;

    private string StateFile => _userData.Combine("plugins.json");

    private HashSet<string> LoadDisabled()
    {
        try
        {
            return File.Exists(StateFile) ? JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(StateFile)) ?? [] : [];
        }
        catch (Exception e) when (e is IOException or JsonException)
        {
            return [];
        }
    }

    private void SaveDisabled() => UserData.WriteAllTextAtomic(StateFile, JsonSerializer.Serialize(_plugins.Where(p => p.State == PluginState.Disabled).Select(p => p.Manifest.Id).ToHashSet()));

    /// <summary>Reads every plugin folder and starts the ones that are not turned off. Plugins already known are stopped and read again.</summary>
    public void Load()
    {
        var disabled = LoadDisabled();
        foreach (var plugin in _plugins.ToList()) Stop(plugin);
        _plugins.Clear();
        Directory.CreateDirectory(Folder);
        foreach (var folder in Directory.EnumerateDirectories(Folder).Order(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(Path.Combine(folder, ManifestName))) continue;
            var plugin = Read(folder);
            _plugins.Add(plugin);
            if (plugin.State == PluginState.Failed) continue;
            if (disabled.Contains(plugin.Manifest.Id)) plugin.State = PluginState.Disabled;
            else Start(plugin);
        }

        Changed?.Invoke();
    }

    private static PluginInfo Read(string folder)
    {
        try
        {
            var manifest = PluginManifest.Parse(File.ReadAllText(Path.Combine(folder, ManifestName)));
            if (!manifest.IsValid) throw new InvalidDataException("plugin.json needs id, name and version.");
            return new PluginInfo(folder, manifest);
        }
        catch (Exception e) when (e is IOException or JsonException or InvalidDataException)
        {
            return new PluginInfo(folder, new PluginManifest { Id = Path.GetFileName(folder), Name = Path.GetFileName(folder) }) { State = PluginState.Failed, Error = e.Message };
        }
    }

    public void Enable(PluginInfo plugin)
    {
        if (plugin.State == PluginState.Active) return;
        Start(plugin);
        SaveDisabled();
        Changed?.Invoke();
    }

    public void Disable(PluginInfo plugin)
    {
        Stop(plugin);
        plugin.State = PluginState.Disabled;
        plugin.Error = null;
        SaveDisabled();
        Changed?.Invoke();
    }

    /// <summary>Stops a plugin, reads its folder again and starts it.</summary>
    public void Reload(PluginInfo plugin)
    {
        Stop(plugin);
        var fresh = Read(plugin.Folder);
        plugin.Manifest = fresh.Manifest;
        plugin.State = fresh.State;
        plugin.Error = fresh.Error;
        if (fresh.State != PluginState.Failed) Start(plugin);
        Changed?.Invoke();
    }

    private void Start(PluginInfo plugin)
    {
        plugin.Error = null;
        try
        {
            var bytes = Compile(plugin);
            var context = new AssemblyLoadContext(plugin.Manifest.Id, isCollectible: true);
            plugin.Context = context;
            var libs = Path.Combine(plugin.Folder, "lib");
            context.Resolving += (_, name) => File.Exists(Path.Combine(libs, name.Name + ".dll")) ? context.LoadFromAssemblyPath(Path.Combine(libs, name.Name + ".dll")) : null;
            using var stream = new MemoryStream(bytes);
            var assembly = context.LoadFromStream(stream);
            var type = assembly.GetTypes().FirstOrDefault(t => typeof(IStudioPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
                ?? throw new InvalidOperationException("No class in the plugin implements IStudioPlugin.");
            plugin.Instance = (IStudioPlugin)(Activator.CreateInstance(type) ?? throw new InvalidOperationException($"{type.Name} could not be created."));
            plugin.Instance.Activate(new Context(this, plugin));
            plugin.State = PluginState.Active;
        }
        catch (Exception e)
        {
            Stop(plugin);
            plugin.State = PluginState.Failed;
            plugin.Error = e is TargetInvocationException { InnerException: { } inner } ? inner.Message : e.Message;
            plugin.Log.Add(e.ToString());
            _notify(NotificationLevel.Error, $"'{plugin.Manifest.Name}' failed to start and was turned off: {plugin.Error}");
        }
    }

    private void Stop(PluginInfo plugin)
    {
        try
        {
            plugin.Instance?.Deactivate();
        }
        catch (Exception e)
        {
            plugin.Log.Add(e.ToString());
        }

        foreach (var id in plugin.CommandIds) _registry.Remove(id);
        plugin.CommandIds.Clear();
        plugin.Editors.Clear();
        plugin.Instance = null;
        plugin.Context?.Unload();
        plugin.Context = null;
    }

    // The assembly of a plugin is cached by the hash of its sources and libraries, so a start without changes does not compile again.
    private byte[] Compile(PluginInfo plugin)
    {
        var sources = Directory.EnumerateFiles(Path.Combine(plugin.Folder, "src"), "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal).ToList();
        if (sources.Count == 0) throw new FileNotFoundException("The plugin has no .cs files under src.");
        var libs = Directory.Exists(Path.Combine(plugin.Folder, "lib")) ? Directory.EnumerateFiles(Path.Combine(plugin.Folder, "lib"), "*.dll").Order(StringComparer.Ordinal).ToList() : [];
        using var hash = SHA256.Create();
        foreach (var file in sources.Concat(libs)) hash.TransformBlock(File.ReadAllBytes(file), 0, (int)new FileInfo(file).Length, null, 0);
        hash.TransformFinalBlock([], 0, 0);
        var cache = _userData.Combine("plugin-cache", plugin.Manifest.Id + "-" + Convert.ToHexString(hash.Hash ?? [])[..16] + ".dll");
        if (File.Exists(cache)) return File.ReadAllBytes(cache);

        var trees = sources.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f, encoding: Encoding.UTF8));
        var references = HostReferences.Value.Concat(libs.Select(l => MetadataReference.CreateFromFile(l)));
        var compilation = CSharpCompilation.Create("Plugin_" + new string([.. plugin.Manifest.Id.Where(char.IsLetterOrDigit)]), trees, references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optimizationLevel: OptimizationLevel.Release, nullableContextOptions: NullableContextOptions.Enable));
        using var output = new MemoryStream();
        var result = compilation.Emit(output);
        if (!result.Success)
        {
            var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Take(5).Select(d => d.ToString());
            throw new InvalidOperationException(string.Join(Environment.NewLine, errors));
        }

        Directory.CreateDirectory(_userData.Combine("plugin-cache"));
        File.WriteAllBytes(cache, output.ToArray());
        return output.ToArray();
    }

    /// <summary>The commit id in the top folder name of a GitHub ZIP ("owner-repo-abc123"), or an empty line for any other source.</summary>
    private static string ZipRevision(string source)
    {
        if (Directory.Exists(source)) return "";
        using var zip = ZipFile.OpenRead(source);
        var top = ZipRoot(zip).TrimEnd('/');
        return top.Length > 0 && top.LastIndexOf('-') is var dash and >= 0 ? top[(dash + 1)..] : "";
    }

    /// <summary>"" when plugin.json is in the ZIP root, or "folder/" when the whole plugin sits in one top folder (a GitHub download does).</summary>
    private static string ZipRoot(ZipArchive zip)
    {
        if (zip.GetEntry(ManifestName) is not null) return "";
        var tops = zip.Entries.Select(e => e.FullName.Split('/')[0]).Distinct().ToList();
        return tops.Count == 1 && zip.GetEntry(tops[0] + "/" + ManifestName) is not null ? tops[0] + "/" : "";
    }

    /// <summary>Reads the manifest of a plugin folder or ZIP without installing it, so the trust prompt can show what is about to be installed.</summary>
    public static PluginManifest Inspect(string source)
    {
        string json;
        if (Directory.Exists(source))
        {
            json = File.ReadAllText(Path.Combine(source, ManifestName));
        }
        else
        {
            using var zip = ZipFile.OpenRead(source);
            var entry = zip.GetEntry(ZipRoot(zip) + ManifestName) ?? throw new InvalidDataException("The ZIP has no plugin.json in its root.");
            using var reader = new StreamReader(entry.Open());
            json = reader.ReadToEnd();
        }

        var manifest = PluginManifest.Parse(json);
        if (!manifest.IsValid) throw new InvalidDataException("plugin.json needs id, name and version.");
        if (manifest.Id.Any(c => !char.IsLetterOrDigit(c) && c is not ('.' or '-' or '_'))) throw new InvalidDataException("The plugin id may only hold letters, digits, dots, dashes and underscores.");
        return manifest;
    }

    /// <summary>Copies a plugin folder or unpacks a ZIP into the plugins folder, replacing an installed plugin with the same id, and loads it. Call after the user accepted the trust prompt.</summary>
    public PluginInfo Install(string source, string? origin = null)
    {
        var manifest = Inspect(source);
        if (_plugins.FirstOrDefault(p => p.Manifest.Id == manifest.Id) is { } old) Stop(old);
        var target = Path.Combine(Folder, manifest.Id);
        if (Directory.Exists(target)) Directory.Delete(target, true);
        Directory.CreateDirectory(target);
        try
        {
            if (Directory.Exists(source))
            {
                foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
                foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), true);
            }
            else
            {
                using var zip = ZipFile.OpenRead(source);
                var root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
                var prefix = ZipRoot(zip);
                foreach (var entry in zip.Entries.Where(e => e.FullName.Length > prefix.Length && e.FullName.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    var path = Path.GetFullPath(Path.Combine(target, entry.FullName[prefix.Length..]));
                    if (!path.StartsWith(root, StringComparison.Ordinal)) throw new InvalidDataException($"'{entry.FullName}' points outside the plugin folder.");
                    if (entry.FullName.EndsWith('/')) Directory.CreateDirectory(path);
                    else
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? target);
                        entry.ExtractToFile(path, true);
                    }
                }
            }

        }
        catch
        {
            Directory.Delete(target, true);
            throw;
        }

        if (origin is not null) File.WriteAllLines(Path.Combine(target, ".origin"), [origin, ZipRevision(source)]);
        Load();
        return _plugins.First(p => p.Manifest.Id == manifest.Id);
    }

    /// <summary>Creates a plugin folder with a manifest and one command, ready to edit. Returns the folder.</summary>
    public string CreateTemplate(string name)
    {
        var id = new string([.. name.Where(char.IsLetterOrDigit)]).ToLowerInvariant();
        if (id.Length == 0) throw new ArgumentException("The name needs a letter or a digit.", nameof(name));
        var folder = Path.Combine(Folder, id);
        if (Directory.Exists(folder)) throw new IOException($"A plugin folder '{id}' already exists.");
        Directory.CreateDirectory(Path.Combine(folder, "src"));
        File.WriteAllText(Path.Combine(folder, ManifestName), JsonSerializer.Serialize(new PluginManifest { Id = id, Name = name, Version = "0.1.0", Description = "A new plugin." }, PluginManifest.Options));
        File.WriteAllText(Path.Combine(folder, "src", "Main.cs"), $$"""
            using G3MStudio.Core.Notifications;
            using G3MStudio.Core.Plugins;

            public sealed class Main : IStudioPlugin
            {
                public void Activate(IStudioContext context) =>
                    context.AddCommand("hello", "{{name}}: Say Hello", () => context.Notify(NotificationLevel.Info, "Hello from {{name}}."));

                public void Deactivate()
                {
                }
            }
            """);
        return folder;
    }

    private sealed class Context(PluginHost host, PluginInfo plugin) : IStudioContext
    {
        public Workspace.Workspace? Workspace => host._workspace();

        public void AddCommand(string id, string title, Action run)
        {
            var full = "plugin." + plugin.Manifest.Id + "." + id;
            host._registry.Add(new Command(full, null, () => Guard(run), Title: title));
            plugin.CommandIds.Add(full);
        }

        public void AddEditor(string typeKey, Func<object, object?> create) => plugin.Editors.Add((typeKey, create));

        public void AddPanel(string id, string title, Func<object> create) => AddCommand("panel." + id, title, () => host.PanelRequested?.Invoke(title, create));

        public void Notify(NotificationLevel level, string message) => host._notify(level, message);

        public void Log(string message) => plugin.Log.Add(message);

        // A failing command reports its error and leaves the app running.
        private void Guard(Action run)
        {
            try
            {
                run();
            }
            catch (Exception e)
            {
                plugin.Log.Add(e.ToString());
                host._notify(NotificationLevel.Error, $"'{plugin.Manifest.Name}': {e.Message}");
            }
        }
    }
}
