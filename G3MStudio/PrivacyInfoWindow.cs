using Avalonia.Controls;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio;

/// <summary>What is sent when crash reports and usage statistics are on, and what never is: one list each, the same text for the welcome window and Settings.</summary>
public sealed class PrivacyInfoWindow : Window
{
    public PrivacyInfoWindow()
    {
        this.Tr(TitleProperty, "privacy.title");
        Width = 520;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        TextBlock Body(string key) => new TextBlock { Name = "Privacy_" + key.Replace('.', '_'), TextWrapping = Avalonia.Media.TextWrapping.Wrap }.Tr(TextBlock.TextProperty, key);
        TextBlock Heading(string key) => new TextBlock { Classes = { "accent" } }.Tr(TextBlock.TextProperty, key);
        DialogFrame.Show(this, new StackPanel
        {
            Spacing = 12,
            Children =
            {
                Body("privacy.intro"),
                Heading("privacy.crashTitle"),
                Body("privacy.crash"),
                Heading("privacy.usageTitle"),
                Body("privacy.usage"),
                Heading("privacy.neverTitle"),
                Body("privacy.never"),
                Body("privacy.off"),
            },
        }, [DialogFrame.Close(this, "ClosePrivacy")], scroll: DialogScroll.Vertical);
    }

    public static void Open(Window owner) => _ = new PrivacyInfoWindow().ShowDialog(owner);
}
