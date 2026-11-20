using G3MStudio.Core.Notifications;

namespace G3MStudio.Core.Plugins;

/// <summary>The entry class of a plugin. Activate runs when the plugin is turned on or reloaded, Deactivate before it is turned off.</summary>
public interface IStudioPlugin
{
    void Activate(IStudioContext context);

    void Deactivate();
}

public enum PluginSettingKind { Bool, Text, Number, Choice }

/// <summary>One option a plugin offers. The Plugins window shows it under the plugin; the plugin reads the value through <see cref="PluginSettings"/>.</summary>
/// <param name="Key">The name the plugin reads it by.</param>
/// <param name="Label">The text the Plugins window shows.</param>
/// <param name="Kind">The control: a check box, a text box, a number box or a list of choices.</param>
/// <param name="Default">The value before the user changes it: "true" or "false" for a Bool, the text itself, the number written out, one of the Choices.</param>
/// <param name="Description">A line of help under the control.</param>
/// <param name="Choices">The values of a Choice option.</param>
/// <param name="Min">The lowest value of a Number option.</param>
/// <param name="Max">The highest value of a Number option.</param>
public sealed record PluginSetting(string Key, string Label, PluginSettingKind Kind, string Default, string Description = "", string[]? Choices = null, double Min = 0, double Max = 1000);

/// <summary>The saved values of one plugin's options (kept in the user data folder, so they survive a reload and an update of the plugin).</summary>
public sealed class PluginSettings
{
    private readonly Dictionary<string, PluginSetting> _definitions = [];
    private readonly Dictionary<string, string> _values;
    private readonly Action<IReadOnlyDictionary<string, string>> _save;

    internal PluginSettings(Dictionary<string, string> values, Action<IReadOnlyDictionary<string, string>> save) => (_values, _save) = (values, save);

    public IReadOnlyCollection<PluginSetting> Definitions => _definitions.Values;

    /// <summary>Raised after a value changed, from the Plugins window or from the plugin itself. Code hints and other views refresh when it fires.</summary>
    public event Action? Changed;

    internal void Define(PluginSetting setting) => _definitions[setting.Key] = setting;

    public string Get(string key) => _values.TryGetValue(key, out var value) ? value : _definitions.TryGetValue(key, out var definition) ? definition.Default : "";

    public bool GetBool(string key) => Get(key).Equals("true", StringComparison.OrdinalIgnoreCase);

    public double GetNumber(string key) => double.TryParse(Get(key), System.Globalization.CultureInfo.InvariantCulture, out var value) ? value : 0;

    public void Set(string key, string value)
    {
        if (Get(key) == value) return;
        _values[key] = value;
        _save(_values);
        Changed?.Invoke();
    }

    public void Reset(string key)
    {
        if (_values.Remove(key))
        {
            _save(_values);
            Changed?.Invoke();
        }
    }
}

/// <summary>Something that happened in the app that a plugin asked to hear about (<see cref="IStudioContext.On"/>).</summary>
/// <param name="Name">workspace.opened (a data file was opened or switched to).</param>
/// <param name="Entry">The code entry, for events about one entry.</param>
public sealed record PluginEvent(string Name, string? Entry = null);

/// <summary>A note shown in gray after the end of a line of code. It is not part of the text: it is never saved and never compiled.</summary>
/// <param name="Line">One based line number.</param>
/// <param name="Text">The note.</param>
/// <param name="Tooltip">Shown when the pointer rests on the note.</param>
public sealed record CodeHint(int Line, string Text, string? Tooltip = null);

/// <summary>What a code hint provider or code action is asked about: the entry, its text as shown in the editor and the selection (zero length when nothing is selected).</summary>
public sealed record CodeRequest(string Entry, string Text, int SelectionStart = 0, int SelectionLength = 0);

/// <summary>What a plugin can use. Everything a plugin adds goes away when it is deactivated.</summary>
public interface IStudioContext
{
    /// <summary>The open data file, or null. Edits go through <see cref="Workspace.Workspace.Edit"/> so they can be undone.</summary>
    Workspace.Workspace? Workspace { get; }

    /// <summary>Adds a command to the palette and the Plugins menu. The id only needs to be unique inside the plugin.</summary>
    void AddCommand(string id, string title, Action run);

    /// <summary>Adds a panel: a command that opens a window whose content <paramref name="create"/> builds (an Avalonia control). The content is built each time the panel opens.</summary>
    void AddPanel(string id, string title, Func<object> create);

    /// <summary>
    /// Adds an editor view for a resource type (the Explorer type key such as "sprites" or "objects"). <paramref name="create"/> gets the resource model
    /// and returns an Avalonia control that is shown above the property list in the resource's tab; return null to show nothing for that resource.
    /// </summary>
    void AddEditor(string typeKey, Func<object, object?> create);

    void Notify(NotificationLevel level, string message);

    /// <summary>Writes a line to the plugin's log, which the Plugins window shows.</summary>
    void Log(string message);

    /// <summary>The plugin's saved options. Declare them with <see cref="AddSetting"/> in Activate.</summary>
    PluginSettings Settings { get; }

    /// <summary>Declares an option: the Plugins window shows a control for it and keeps the value. Declaring the same key again replaces the definition.</summary>
    void AddSetting(PluginSetting setting);

    /// <summary>Calls <paramref name="handler"/> when the named thing happens (<see cref="PluginEvent"/>). A handler that throws is logged and skipped.</summary>
    void On(string eventName, Action<PluginEvent> handler);

    /// <summary>
    /// Adds a provider of gray notes after the end of lines of code. It runs on the UI thread a moment after the text changes, when the plugin reloads and when
    /// its settings change, so keep it quick (read files once and cache them).
    /// </summary>
    void AddCodeHints(Func<CodeRequest, IEnumerable<CodeHint>> provider);

    /// <summary>Adds an entry to the code editor's context menu. <paramref name="run"/> gets the text and selection and returns the whole new text, or null to change nothing. One undo step.</summary>
    void AddCodeAction(string id, string title, Func<CodeRequest, string?> run);

    /// <summary>The names of the code entries that have text of their own (child entries are part of their parent).</summary>
    IReadOnlyList<string> CodeEntries { get; }

    /// <summary>The decompiled text of an entry as the editor shows it, or null when it cannot be decompiled.</summary>
    string? GetCode(string entry);

    /// <summary>Replaces the code of an entry (compiles it first). Returns null on success or the error text. One undo step; a failed compile changes nothing.</summary>
    Task<string?> SetCodeAsync(string entry, string text);

    /// <summary>The folder the open data file is in (where the game's own files such as lang/ are), or null when no file is open.</summary>
    string? GameFolder { get; }
}
