using Avalonia.Controls;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>
/// Help > Send Feedback: a message, an optional contact and a box for the diagnostics. The window says exactly what goes out. It closes with true once the message was handed to
/// the send callback; with reporting off the text is shown but cannot be sent.
/// </summary>
public sealed class FeedbackWindow : Window
{
    public FeedbackWindow(bool reportingOn, Func<string, string?, bool, bool> send)
    {
        this.Tr(TitleProperty, "feedback.title");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        CanResize = false;
        var message = new TextBox { Name = "FeedbackMessage", AcceptsReturn = true, TextWrapping = Avalonia.Media.TextWrapping.Wrap, MinHeight = 120, MaxLength = 4000 };
        var contact = new TextBox { Name = "FeedbackContact", MaxLength = 200 };
        var diagnostics = new CheckBox { Name = "FeedbackDiagnostics", IsChecked = true }.Tr(ContentControl.ContentProperty, "feedback.diagnostics");
        var submit = DialogFrame.Wire(this, new Button { Name = "FeedbackSend", IsEnabled = false }.Tr(ContentControl.ContentProperty, "feedback.send"), () =>
        {
            if (send(message.Text ?? "", contact.Text, diagnostics.IsChecked == true)) Close(true);
        }, DialogRole.Primary);
        message.TextChanged += (_, _) => submit.IsEnabled = reportingOn && !string.IsNullOrWhiteSpace(message.Text);
        Avalonia.Automation.AutomationProperties.SetName(message, Loc.Current["feedback.message"]);
        Avalonia.Automation.AutomationProperties.SetName(contact, Loc.Current["feedback.contact"]);
        static TextBlock Note(string key, string name) => new TextBlock { Name = name, Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, key);
        DialogFrame.Show(this, new StackPanel
        {
            Spacing = 12,
            Children =
            {
                new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, "feedback.message"),
                message,
                new TextBlock { TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, "feedback.contact"),
                contact,
                diagnostics,
                Note("feedback.sends", "FeedbackSends"),
                new TextBlock { Name = "FeedbackOff", IsVisible = !reportingOn, TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, "feedback.off"),
            },
        }, [DialogFrame.Cancel(this), submit], scroll: DialogScroll.Vertical);
    }
}
