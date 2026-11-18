using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Editor;
using G3MStudio.Rooms;
using G3MStudio.Localization;

#pragma warning disable CA2000 // windows and views made for a walk only

namespace G3MStudio.Tests;

/// <summary>Every button that shows no word (a glyph, an icon) must still be announced and explained: an automation name and a tooltip.</summary>
public sealed partial class AccessibilityTests(ITestOutputHelper output)
{
    [GeneratedRegex(@"\p{L}{2,}")]
    private static partial Regex Word();

    private static string TextOf(Control control) => string.Concat(new[] { control }.Concat(control.GetVisualDescendants().OfType<Control>()).Select(c => c is TextBlock t ? t.Text : null));

    private static bool IconOnly(ContentControl button) => button.Content switch
    {
        null => true,
        string s => !Word().IsMatch(s),
        Control c => !Word().IsMatch(TextOf(c)),
        _ => false,
    };

    /// <summary>Icon-only buttons that miss a name or a tooltip. Parts of library templates (spinners, scroll bars, combo arrows) are not ours to name.</summary>
    internal static List<string> Offenders(Visual root)
    {
        Dispatcher.UIThread.RunJobs();
        (root as TopLevel)?.UpdateLayout();
        return [.. root.GetVisualDescendants().OfType<ContentControl>().Where(b => b is Button or ToggleButton)
            .Where(b => b.IsEffectivelyVisible && b.GetVisualAncestors().OfType<Control>().All(a => a is not (NumericUpDown or ComboBox or ScrollBar or Slider or TabControl)))
            .Where(IconOnly)
            .Where(b => string.IsNullOrWhiteSpace(AutomationProperties.GetName(b)) || ToolTip.GetTip(b) is null)
            .Select(b => $"{b.GetType().Name} '{b.Name}' in {b.GetVisualAncestors().OfType<Control>().Select(a => a.Name).FirstOrDefault(n => !string.IsNullOrEmpty(n))}")];
    }

    [Fact]
    public async Task EveryIconOnlyButtonOfTheMainWindowAndTheKeyEditorsHasANameAndATooltip()
    {
        using var dir = new TempDir();
        string path;
        using (var data = TestData.Create(3, true))
        {
            TestData.AddPage(data);
            SpriteTemplate.Create(data, "spr_a");
            path = TestData.Write(dir.File("data.win"), data);
        }

        var userData = new UserData(dir.File("user"));
        await HeadlessApp.Run(async () =>
        {
            Loc.Current.SetLanguage("en");
            var window = new MainWindow(new WorkspaceService(userData, RecentFiles.Load(userData)));
            window.Show();
            try
            {
                var found = Offenders(window);
                await window.OpenAsync(path);
                for (var i = 0; i < 300 && window.Workspaces.Current is null; i++)
                {
                    Dispatcher.UIThread.RunJobs();
                    await Task.Delay(10);
                }

                var workspace = window.Workspaces.Current!;
                found.AddRange(Offenders(window));
                // A document tab brings the tab strip with its close button and the history buttons.
                window.Docking.OpenTab("code", ResourceTypes.KeyOf("code", workspace.Data.Code.First().Name.Content!), false);
                found.AddRange(Offenders(window));

                var sprite = workspace.Data.Sprites.First(s => s.Name.Content == "spr_a");
                using var textures = new TextureBitmaps();
                using var session = new WorkspaceImageSession(workspace, "spr_a");
                using var image = new ImageEditorView(session, 0, new ImageOptions());
                var views = new Control[] { new SpriteEditorView(sprite, textures, workspace), image };
                foreach (var view in views)
                {
                    var host = new Window { Content = view, Width = 1200, Height = 700 };
                    host.Show();
                    found.AddRange(Offenders(host));
                    host.Close();
                }

                foreach (var line in found.Distinct()) output.WriteLine(line);
                Assert.Empty(found.Distinct());
            }
            finally { window.Close(); }
        });
    }
}
