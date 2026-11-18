using Avalonia.Controls;

namespace G3MStudio;

/// <summary>Windows that sit beside the main window: one of each kind, and the ones that work on the open file go when that file does.</summary>
public sealed partial class MainWindow
{
    private const string PerFile = "file:";

    private readonly Dictionary<string, Window> _tools = [];

    /// <summary>Shows the window of a kind, or brings the one already open forward. A key that starts with <see cref="PerFile"/> names a window of the open file.</summary>
    private Window ShowTool(string key, Func<Window> create)
    {
        if (_tools.TryGetValue(key, out var open) && open.IsVisible)
        {
            open.Activate();
            return open;
        }

        var window = _tools[key] = create();
        window.Closed += (_, _) =>
        {
            if (_tools.TryGetValue(key, out var current) && current == window) _tools.Remove(key);
        };
        window.Show(this);
        return window;
    }

    /// <summary>The file changed or closed: windows that edit its model would act on one that is gone.</summary>
    private void CloseFileTools()
    {
        foreach (var (key, window) in _tools.ToList())
        {
            if (key.StartsWith(PerFile, StringComparison.Ordinal)) window.Close();
        }
    }
}
