using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

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
        var message = Loc.Current.Format(prompt.ProjectMode ? "prompt.project" : "prompt.direct", ("name", prompt.Name), ("count", prompt.Changes));
        Button Choice(string key, CloseChoice choice, string name)
        {
            var button = new Button { Content = Loc.Current[key], Name = name };
            button.Click += (_, _) => Close(choice);
            return button;
        }

        Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(16),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children =
                    {
                        Choice(prompt.ProjectMode ? "prompt.sync" : "prompt.save", CloseChoice.Save, "Save"),
                        Choice("prompt.later", CloseChoice.DoItLater, "Later"),
                        Choice("explorer.cancel", CloseChoice.Cancel, "Cancel"),
                    },
                },
            },
        };
    }

    /// <summary>Closing the dialog with its own close button counts as Cancel.</summary>
    public async Task<CloseChoice> AskAsync(Window owner) => await ShowDialog<CloseChoice?>(owner) ?? CloseChoice.Cancel;
}
