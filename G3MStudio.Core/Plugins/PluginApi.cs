using G3MStudio.Core.Notifications;

namespace G3MStudio.Core.Plugins;

/// <summary>The entry class of a plugin. Activate runs when the plugin is turned on or reloaded, Deactivate before it is turned off.</summary>
public interface IStudioPlugin
{
    void Activate(IStudioContext context);

    void Deactivate();
}

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
}
