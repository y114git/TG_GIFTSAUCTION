using System.IO.Compression;
using System.Reflection;
using System.Runtime.InteropServices;
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

    internal List<Func<CodeRequest, IEnumerable<CodeHint>>> Hints { get; } = [];

    internal List<(string Id, string Title, Func<CodeRequest, string?> Run)> CodeActions { get; } = [];

    internal List<(string Name, Action<PluginEvent> Handler)> Handlers { get; } = [];

    /// <summary>The options the plugin declared and their saved values; null while the plugin is not running.</summary>
    public PluginSettings? Settings { get; internal set; }

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

    /// <summary>The code hints may look different now: a plugin started or stopped, or one of its options changed. Open code editors read them again.</summary>
    public event Action? HintsChanged;

    /// <summary>Whether plugins run at all (the global option). Turning it off stops every plugin without forgetting which ones were enabled.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Whether any active plugin adds code hints; editors skip their timers and text reads when none does.</summary>
    public bool HasHints => Enabled && _plugins.Any(p => p.State == PluginState.Active && p.Hints.Count > 0);

    /// <summary>The gray notes every active plugin wants after the lines of this text. A provider that throws is logged and skipped.</summary>
    public IReadOnlyList<CodeHint> CollectHints(string entry, string text)
    {
        var all = new List<CodeHint>();
        if (!Enabled) return all;
        foreach (var plugin in _plugins.Where(p => p.State == PluginState.Active).ToList())
            foreach (var provider in plugin.Hints.ToList())
            {
                try
                {
                    all.AddRange(provider(new CodeRequest(entry, text)));
                }
                catch (Exception e)
                {
                    plugin.Log.Add("Code hints: " + e);
                }
            }

        return all;
    }

    public IReadOnlyList<(string Title, Func<CodeRequest, string?> Run)> CodeActions =>
        Enabled ? [.. _plugins.Where(p => p.State == PluginState.Active).SelectMany(p => p.CodeActions.Select(a => (a.Title, Guarded(p, a.Run))))] : [];

    private static Func<CodeRequest, string?> Guarded(PluginInfo plugin, Func<CodeRequest, string?> run) => request =>
    {
        try
        {
            return run(request);
        }
        catch (Exception e)
        {
            plugin.Log.Add(e.ToString());
            return null;
        }
    };

    /// <summary>Tells the plugins that asked to hear about this event.</summary>
    public void Notify(PluginEvent e)
    {
        if (!Enabled) return;
        foreach (var plugin in _plugins.Where(p => p.State == PluginState.Active).ToList())
            foreach (var (name, handler) in plugin.Handlers.Where(h => h.Name == e.Name).ToList())
            {
                try
                {
                    handler(e);
                }
                catch (Exception ex)
                {
                    plugin.Log.Add($"{e.Name}: {ex}");
                }
            }
    }

    private string SettingsFile(PluginInfo plugin) => _userData.Combine("plugin-settings", plugin.Manifest.Id + ".json");

    private PluginSettings OpenSettings(PluginInfo plugin)
    {
        Dictionary<string, string> values = [];
        try
        {
            if (File.Exists(SettingsFile(plugin))) values = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(SettingsFile(plugin))) ?? [];
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            plugin.Log.Add("The saved options could not be read and are ignored: " + e.Message);
        }

        var settings = new PluginSettings(values, saved =>
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile(plugin)) ?? ".");
                UserData.WriteAllTextAtomic(SettingsFile(plugin), JsonSerializer.Serialize(saved));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                plugin.Log.Add("The options could not be saved: " + e.Message);
            }
        });
        settings.Changed += () => HintsChanged?.Invoke();
        return settings;
    }

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
            if (!File.Exists(Path.Combine(folder, ManifestName)) || Path.GetFileName(folder).StartsWith(StagePrefix, StringComparison.Ordinal)) continue; // an install in progress
            var plugin = Read(folder);
            _plugins.Add(plugin);
            if (plugin.State == PluginState.Failed) continue;
            if (disabled.Contains(plugin.Manifest.Id) || !Enabled) plugin.State = PluginState.Disabled;
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

    /// <summary>
    /// Like <see cref="Reload"/>, but the compile of a changed source runs on a pool thread, so the window keeps responding.
    /// Call on the thread that owns the host; the plugin stops and starts there once the compile is done.
    /// </summary>
    public Task ReloadAsync(PluginInfo plugin) => _lastReload = ReloadAfter(_lastReload, plugin);

    // Reloads run one after the other, so a second save never starts while the first compile is still going.
    private Task _lastReload = Task.CompletedTask;

    private async Task ReloadAfter(Task previous, PluginInfo plugin)
    {
        await previous;
        var fresh = Read(plugin.Folder);
        byte[]? bytes = null;
        Exception? error = null;
        if (fresh.State != PluginState.Failed)
        {
            try
            {
                bytes = await Task.Run(() => Compile(fresh));
            }
            catch (Exception e)
            {
                error = e;
            }
        }

        Stop(plugin);
        plugin.Manifest = fresh.Manifest;
        plugin.State = fresh.State;
        plugin.Error = fresh.Error;
        if (fresh.State != PluginState.Failed) Start(plugin, bytes, error);
        Changed?.Invoke();
    }

    private void Start(PluginInfo plugin, byte[]? compiled = null, Exception? compileError = null)
    {
        plugin.Error = null;
        try
        {
            if (compileError is not null) throw compileError;
            var bytes = compiled ?? Compile(plugin);
            var context = new AssemblyLoadContext(plugin.Manifest.Id, isCollectible: true);
            plugin.Context = context;
            var libs = Path.Combine(plugin.Folder, "lib");
            context.Resolving += (_, name) => File.Exists(Path.Combine(libs, name.Name + ".dll")) ? context.LoadFromAssemblyPath(Path.Combine(libs, name.Name + ".dll")) : null;
            using var stream = new MemoryStream(bytes);
            plugin.Settings = OpenSettings(plugin);
            var assembly = context.LoadFromStream(stream);
            var type = assembly.GetTypes().FirstOrDefault(t => typeof(IStudioPlugin).IsAssignableFrom(t) && t is { IsAbstract: false, IsInterface: false })
                ?? throw new InvalidOperationException("No class in the plugin implements IStudioPlugin.");
            plugin.Instance = (IStudioPlugin)(Activator.CreateInstance(type) ?? throw new InvalidOperationException($"{type.Name} could not be created."));
            plugin.Instance.Activate(new Context(this, plugin));
            plugin.State = PluginState.Active;
            HintsChanged?.Invoke();
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
        plugin.Hints.Clear();
        plugin.CodeActions.Clear();
        plugin.Handlers.Clear();
        plugin.Settings = null;
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
        var cache = _userData.Combine("plugin-cache", plugin.Manifest.Id + "-" + CacheKey(sources.Concat(libs), typeof(PluginHost).Assembly.ManifestModule.ModuleVersionId) + ".dll");
        if (File.Exists(cache)) return File.ReadAllBytes(cache);

        // The usings every .NET project gets, so a plugin does not need a block of using lines to use List, Task or File.
        var trees = sources.Select(f => CSharpSyntaxTree.ParseText(File.ReadAllText(f), path: f, encoding: Encoding.UTF8))
            .Append(CSharpSyntaxTree.ParseText("global using System; global using System.Collections.Generic; global using System.IO; global using System.Linq; global using System.Threading; global using System.Threading.Tasks;"));
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

    /// <summary>The hash of the sources and libraries plus the build of the host: a plugin compiled against an older G3MStudio.Core is built again after an update.</summary>
    internal static string CacheKey(IEnumerable<string> files, Guid hostBuild)
    {
        using var hash = SHA256.Create();
        var host = Encoding.UTF8.GetBytes($"{hostBuild}|{RuntimeInformation.FrameworkDescription}");
        hash.TransformBlock(host, 0, host.Length, null, 0);
        foreach (var file in files)
        {
            var bytes = File.ReadAllBytes(file);
            hash.TransformBlock(bytes, 0, bytes.Length, null, 0);
        }

        hash.TransformFinalBlock([], 0, 0);
        return Convert.ToHexString(hash.Hash ?? [])[..16];
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
        // An id of dots only would make the install folder the plugins folder or its parent, and the install deletes that folder first.
        if (manifest.Id.All(c => c == '.') || manifest.Id.Any(c => !char.IsLetterOrDigit(c) && c is not ('.' or '-' or '_'))) throw new InvalidDataException("The plugin ID may only hold letters, digits, dots, dashes and underscores.");
        return manifest;
    }

    /// <summary>The prefix of the folders an install works in; no plugin ID can start with it, and <see cref="Load"/> skips them.</summary>
    private const string StagePrefix = "~";

    /// <summary>
    /// Copies a plugin folder or unpacks a ZIP into the plugins folder, replacing an installed plugin with the same id, and loads it. Call after the user accepted the trust prompt.
    /// The new files are built beside the old plugin and swapped in at the end, so a failed update keeps the old plugin as it was.
    /// </summary>
    public PluginInfo Install(string source, string? origin = null)
    {
        var manifest = Inspect(source);
        var target = Path.Combine(Folder, manifest.Id);
        var fresh = Path.Combine(Folder, StagePrefix + "new-" + manifest.Id);
        var backup = Path.Combine(Folder, StagePrefix + "old-" + manifest.Id);
        if (Directory.Exists(source) && IsInsideFolder(source)) throw new InvalidDataException("The plugin folder is inside the plugins folder. Install it from somewhere else.");
        foreach (var leftover in new[] { fresh, backup })
        {
            DeleteFolder(leftover); // what an interrupted install left
            if (Directory.Exists(leftover)) throw new IOException($"'{leftover}' could not be removed.");
        }

        Directory.CreateDirectory(fresh);
        try
        {
            Unpack(source, fresh);
            if (Inspect(fresh).Id != manifest.Id) throw new InvalidDataException("The unpacked plugin does not match its manifest.");
            if (origin is not null) File.WriteAllLines(Path.Combine(fresh, ".origin"), [origin, ZipRevision(source)]);
        }
        catch
        {
            DeleteFolder(fresh);
            throw;
        }

        if (_plugins.FirstOrDefault(p => p.Manifest.Id == manifest.Id) is { } old) Stop(old);
        var replaced = Directory.Exists(target);
        try
        {
            if (replaced) Directory.Move(target, backup);
            Directory.Move(fresh, target);
        }
        catch
        {
            // The old plugin goes back and starts again.
            if (replaced && !Directory.Exists(target) && Directory.Exists(backup)) Directory.Move(backup, target);
            DeleteFolder(fresh);
            Load();
            throw;
        }

        DeleteFolder(backup);
        Load();
        return _plugins.First(p => p.Manifest.Id == manifest.Id);
    }

    private bool IsInsideFolder(string path)
    {
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Folder)) + Path.DirectorySeparatorChar;
        return (Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) + Path.DirectorySeparatorChar).StartsWith(root, OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
    }

    // A folder that cannot be removed now (a program holds a file) is left for the next install to clear.
    private static void DeleteFolder(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void Unpack(string source, string target)
    {
        if (Directory.Exists(source))
        {
            foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories)) Directory.CreateDirectory(Path.Combine(target, Path.GetRelativePath(source, directory)));
            foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories)) File.Copy(file, Path.Combine(target, Path.GetRelativePath(source, file)), true);
            return;
        }

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

        public PluginSettings Settings => plugin.Settings ?? throw new InvalidOperationException("The plugin is not running.");

        public void AddSetting(PluginSetting setting) => Settings.Define(setting);

        public void On(string eventName, Action<PluginEvent> handler) => plugin.Handlers.Add((eventName, handler));

        public void AddCodeHints(Func<CodeRequest, IEnumerable<CodeHint>> provider) => plugin.Hints.Add(provider);

        public void AddCodeAction(string id, string title, Func<CodeRequest, string?> run) => plugin.CodeActions.Add((id, title, run));

        public IReadOnlyList<string> CodeEntries
        {
            get
            {
                if (host._workspace() is not { } workspace) return [];
                using (workspace.Lock.Read()) return [.. workspace.Data.Code.Where(c => c.ParentEntry is null).Select(c => c.Name?.Content).OfType<string>()];
            }
        }

        public string? GetCode(string entry)
        {
            if (host._workspace() is not { } workspace) return null;
            try
            {
                using (workspace.Lock.Read()) return CodeText.Decompile(workspace.Data, entry, workspace.DecompileContext());
            }
            catch (Exception e) when (e is InvalidOperationException or ArgumentException or NullReferenceException or IndexOutOfRangeException)
            {
                return null;
            }
        }

        public async Task<string?> SetCodeAsync(string entry, string text)
        {
            if (host._workspace() is not { } workspace) return "No data file is open.";
            var command = new EditCodeCommand(entry, text);
            if ((await workspace.ProbeInBackground([command])) is [{ } failed, ..]) return failed.Error.Message;
            await workspace.EditInBackground(command);
            return null;
        }

        public string? GameFolder => host._workspace() is { Path: { Length: > 0 } path } ? Path.GetDirectoryName(path) : null;

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
