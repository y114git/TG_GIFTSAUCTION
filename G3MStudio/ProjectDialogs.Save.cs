using Avalonia.Controls;
using G3MStudio.Localization;

namespace G3MStudio;

internal static partial class ProjectDialogs
{
    /// <summary>
    /// The first Save question. Outside Project mode it explains the save and offers Save or Cancel (<c>Sync</c> is null). In Project mode it picks
    /// "Save progress" (project files only, <c>Sync</c> false) or "Save and sync with data file" (<c>Sync</c> true, the Enter button). Null when cancelled; Remember is the box.
    /// </summary>
    public static async Task<(bool? Sync, bool Remember)?> AskSaveAsync(Window owner, bool project)
    {
        var window = Shell("save.askTitle", project ? 520 : 460);
        (bool?, bool)? answer = null;
        var remember = new CheckBox { Name = "DontAskAgain", Content = Loc.Current[project ? "save.remember" : "prompts.dontAsk"] };
        void Done(bool? sync)
        {
            answer = (sync, remember.IsChecked == true);
            window.Close();
        }

        var cancel = Btn(window, "common.cancel", "Cancel", window.Close);
        var buttons = project
            ? Buttons(Btn(window, "save.sync", "SaveSync", () => Done(true)), Btn(window, "save.progress", "SaveProgress", () => Done(false)), cancel)
            : Buttons(Btn(window, "save.go", "Save", () => Done(null)), cancel);
        Frame(window, new StackPanel
        {
            Spacing = Mods.ModsUi.Space(4),
            Children = { new TextBlock { Text = Loc.Current[project ? "save.askProject" : "save.askData"], TextWrapping = Avalonia.Media.TextWrapping.Wrap }, remember, buttons },
        });
        await window.ShowDialog(owner);
        return answer;
    }
}
