using G3MStudio.Theme;
using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>About G3MStudio: version, the library it is built on, the platform and the licence. Copy Version Info puts the same lines on the clipboard.</summary>
public sealed class AboutWindow : Window
{
    public AboutWindow()
    {
        Title = Loc.Current["about.title"];
        Width = 440;
        MinWidth = 340;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var text = new TextBlock { Name = "AboutText", Text = VersionInfo(), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var copy = new Button { Name = "CopyVersion", Content = Loc.Current["about.copy"] };
        copy.Click += async (_, _) =>
        {
            if (Clipboard is { } clipboard) await clipboard.SetTextAsync(VersionInfo());
        };
        DialogFrame.Show(this, new StackPanel
        {
            Spacing = 12,
            Children = { new TextBlock { Text = Loc.Current["app.title"] }.TextSize("FontSize.Display"), text },
        }, [copy, DialogFrame.Close(this, "CloseAbout")], scroll: DialogScroll.Vertical);
    }

    /// <summary>The lines of the dialog, one per fact, in English and the same for every language so a bug report reads the same.</summary>
    public static string VersionInfo()
    {
        var app = typeof(AboutWindow).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";
        var library = typeof(G3MLib.DataFile.GameMakerData).Assembly.GetName().Version?.ToString(3) ?? "unknown";
        return string.Join('\n',
            $"G3MStudio {app.Split('+')[0]}",
            $"G3MLib {library}",
            $"{RuntimeInformation.FrameworkDescription}, {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})",
            "GPL-3.0-only");
    }
}
