using System.Reflection;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Layout;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>About G3MStudio: version, the library it is built on, the platform and the licence. Copy Version Info puts the same lines on the clipboard.</summary>
public sealed class AboutWindow : Window
{
    public AboutWindow()
    {
        Title = Loc.Current["about.title"];
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        var text = new TextBlock { Name = "AboutText", Text = VersionInfo(), TextWrapping = Avalonia.Media.TextWrapping.Wrap };
        var copy = new Button { Name = "CopyVersion", Content = Loc.Current["about.copy"] };
        copy.Click += async (_, _) =>
        {
            if (Clipboard is { } clipboard) await clipboard.SetTextAsync(VersionInfo());
        };
        var close = new Button { Name = "CloseAbout", Content = Loc.Current["settings.close"] };
        close.Click += (_, _) => Close();
        Content = new StackPanel
        {
            Margin = new Thickness(16),
            Spacing = 12,
            Children =
            {
                new TextBlock { Text = Loc.Current["app.title"], FontSize = 20 },
                text,
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, Children = { copy, close } },
            },
        };
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
