using Avalonia.Controls;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>The close and restart prompt of ADR-0010 item 6: Save (Sync changes in Project mode), Do it later, Cancel.</summary>
public sealed class ClosePromptWindow : Window
{
    public ClosePromptWindow(ClosePrompt prompt)
    {
        Title = Loc.Current["prompt.title"];
        Width = 440;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        var dontAsk = new CheckBox { Name = "DontAskAgain", Content = Loc.Current["prompts.dontAskClose"] };
        var message = Loc.Current.Format(prompt.ProjectMode ? "prompt.project" : "prompt.direct", ("name", prompt.Name), ("count", prompt.Changes));
        Button Choice(string key, CloseChoice choice, string name) => DialogFrame.Action(this, Loc.Current[key], name, () =>
        {
            DontAskAgain = choice == CloseChoice.DoItLater && dontAsk.IsChecked == true;
            Close(choice);
        }, choice == CloseChoice.Save ? DialogRole.Primary : choice == CloseChoice.Cancel ? DialogRole.Cancel : DialogRole.Normal);

        DialogFrame.Show(this, new StackPanel { Spacing = 16, Children = { new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, dontAsk } },
            [Choice(prompt.ProjectMode ? "prompt.sync" : "prompt.save", CloseChoice.Save, "Save"), Choice("prompt.later", CloseChoice.DoItLater, "Later"), Choice("common.cancel", CloseChoice.Cancel, "Cancel")],
            scroll: DialogScroll.Vertical);
    }

    /// <summary>The box was checked when the user chose Do it later: from now on closing keeps the changes in the journal without asking.</summary>
    public bool DontAskAgain { get; private set; }

    /// <summary>Closing the dialog with its own close button counts as Cancel.</summary>
    public async Task<CloseChoice> AskAsync(Window owner) => await ShowDialog<CloseChoice?>(owner) ?? CloseChoice.Cancel;
}
