using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>
/// Every dialog at 100% and 200% interface scale in English, Russian and Japanese, at its own size and squeezed to its minimum size: the primary button stays
/// in view outside any scrolling area, every control is inside the window or reachable by scrolling, and no label is cut. Pictures go to G3MSTUDIO_SHOTS.
/// </summary>
public sealed class DialogLayoutTests
{
    [Theory]
    [InlineData(100, "en")]
    [InlineData(100, "ru")]
    [InlineData(200, "en")]
    [InlineData(200, "ru")]
    [InlineData(200, "ja")]
    public async Task EveryDialogKeepsItsButtonsInViewAndItsContentReachable(int scale, string language)
    {
        using var dir = new TempDir();
        var shots = Path.Combine(Corpus.Shots, "dialogs");
        Directory.CreateDirectory(shots);
        var problems = new List<string>();
        await HeadlessApp.Run(async () =>
        {
            var owner = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var file = Corpus.Files(Corpus.Fixtures).First();
                await owner.OpenAsync(file);
                var workspace = owner.Workspaces.Current!;
                owner.Workspaces.Settings.Update(x => x with { UiScale = scale });
                Loc.Current.SetLanguage(language);
                var room = workspace.Data.Rooms.OrderByDescending(r => r.GameObjects.Count).First();
                var entry = workspace.Data.Code[0].Name.Content!;
                var wide = string.Join(' ', Enumerable.Repeat("a_very_long_resource_name", 6));
                Window Ask(Func<Task> ask)
                {
                    var before = owner.OwnedWindows.Count;
                    _ = ask();
                    return owner.OwnedWindows[before];
                }

                var cases = new (string Name, Func<Window> Make)[]
                {
                    ("ClosePrompt", () => new ClosePromptWindow(new ClosePrompt("data.win", 3, false))),
                    ("About", () => new AboutWindow()),
                    ("Layouts", () => new LayoutsWindow(() => ["Wide editor", "Debug"], _ => null, _ => { }, _ => { }, (_, _) => null)),
                    ("Onboarding", () => new OnboardingWindow([.. Loc.Current.Languages], "en", "ru", true, new OnboardingActions(_ => { }, _ => { }, _ => { }, () => { }))),
                    ("Tour", () => new TourWindow(_ => { })),
                    ("Groups", () => new GroupsWindow(workspace, false)),
                    ("Strings", () => new StringsWindow(workspace)),
                    ("RoomOrder", () => new RoomOrderWindow(workspace)),
                    ("CreationOrder", () => new CreationOrderWindow(room, workspace)),
                    ("Hex", () => new HexViewWindow(file)),
                    ("Audio", () => new AudioEditorWindow("Edit audio", ScreenshotTests.ToneWav(), _ => { })),
                    ("Sheet", () => new SheetImportWindow([new PixelBuffer(64, 16)], 4, (16, 16))),
                    ("Resize", () => new Editor.ResizeImageWindow((32, 32), 3, ["Default", "Other"], "Default", false)),
                    ("PackInfo", () => new Mods.PackInfoWindow(3, "Pack")),
                    ("BulkRename", () => new BulkRenameWindow([("sprites", "spr_a"), ("sprites", "spr_b")], (_, _) => false, _ => null)),
                    ("GoTo", () => new GoToWindow(_ => [], _ => Task.FromResult(""))),
                    ("Parameter", () => Ask(() => Editor.ParameterWindow.AskAsync(owner, "Rotate", [("Angle of the rotation", 90, 0, 360), ("Another long parameter name", 1, 0, 10)]))),
                    ("ProjectLocation", () => Ask(() => ProjectDialogs.AskLocationAsync(owner, @"C:\Users\someone\Documents\G3MStudio projects"))),
                    ("ProjectSettings", () => Ask(() => ProjectDialogs.AskSettingsAsync(owner, new ProjectSettings()))),
                    ("ProjectMove", () => Ask(() => ProjectDialogs.AskMoveAsync(owner, @"C:\projects\one", (_, _) => Task.FromResult<string?>(null)))),
                    ("ProjectDisable", () => Ask(() => ProjectDialogs.AskDisableAsync(owner))),
                    ("ProjectExternal", () => Ask(() => ProjectDialogs.AskExternalChangeAsync(owner, "data.win"))),
                    ("ProjectConflicts", () => Ask(() => ProjectDialogs.AskConflictsAsync(owner, [new MergeConflict(wide, "sprites", "a"), new MergeConflict("objects/obj_b.json", "objects", "b")]))),
                    ("ProjectConfirm", () => Ask(() => ProjectDialogs.AskConfirmAsync(owner, "Confirm", wide + " " + wide))),
                    ("ProjectRemember", () => Ask(() => ProjectDialogs.AskConfirmRememberAsync(owner, "Confirm", "Do the thing?"))),
                    ("ProjectMoment", () => Ask(() => ProjectDialogs.AskMomentCopyAsync(owner))),
                    ("ProjectText", () => Ask(() => ProjectDialogs.AskTextAsync(owner, "Text", "Enter text", "", true))),
                    ("ProjectChoice", () => Ask(() => ProjectDialogs.AskChoiceAsync(owner, "Choose", "Pick one", ["2024.1\tC:\\runtimes\\a", "2023.8\tC:\\runtimes\\b"], ["Version", "Path"], 0))),
                    ("ProjectRebuild", () => Ask(() => ProjectDialogs.AskRebuildAsync(owner, "data.win"))),
                };
                foreach (var (name, make) in cases)
                {
                    var tag = $"{name} {scale}% {language}";
                    var dialog = make();
                    if (!dialog.IsVisible) dialog.Show(owner);
                    await Settle();
                    problems.AddRange(Audit(dialog).Select(p => $"{tag}: {p}"));
                    Shoot(dialog, Path.Combine(shots, $"{name}-{scale}-{language}.png"));

                    // The smallest the dialog can be made: a window that sizes itself is limited in height, any other is set to its minimum.
                    if (dialog.SizeToContent != SizeToContent.Manual) dialog.MaxHeight = Math.Max(dialog.MinHeight, 220 * scale / 100.0);
                    else if (dialog.CanResize && dialog.MinWidth > 0 && dialog.MinHeight > 0) (dialog.Width, dialog.Height) = (dialog.MinWidth, dialog.MinHeight);
                    await Settle();
                    problems.AddRange(Audit(dialog).Select(p => $"{tag} (smallest): {p}"));
                    Shoot(dialog, Path.Combine(shots, $"{name}-{scale}-{language}-small.png"));
                    dialog.Close();
                    await Settle();
                }
            }
            finally
            {
                Loc.Current.SetLanguage("en");
                owner.Close();
            }
        });
        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems.Distinct()));
    }

    [Fact]
    public async Task EscapeAsksBeforeItDropsWhatWasTyped()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var owner = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var rename = new RenameWindow("spr_old", _ => null);
                rename.Show(owner);
                await Settle();
                rename.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null); // nothing typed: closes at once
                await Settle();
                Assert.False(rename.IsVisible);

                var typed = new RenameWindow("spr_old", _ => null);
                typed.Show(owner);
                await Settle();
                typed.GetVisualDescendants().OfType<TextBox>().Single(b => b.Name == "NewName").Text = "spr_new";
                typed.KeyPress(Avalonia.Input.Key.Escape, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Escape, null);
                await Settle();
                Assert.True(typed.IsVisible); // kept open, with the question on top
                var question = typed.OwnedWindows.Single();
                Assert.Equal(Loc.Current["common.discardTitle"], question.Title);
                question.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "KeepEditing").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await Settle();
                Assert.True(typed.IsVisible);
                typed.Close();
            }
            finally { owner.Close(); }
        });
    }

    private static async Task Settle()
    {
        for (var i = 0; i < 4; i++)
        {
            await Task.Delay(30);
            Dispatcher.UIThread.RunJobs();
        }
    }

    private static void Shoot(Window window, string path)
    {
        if (window.Bounds.Width < 1 || window.Bounds.Height < 1) return;
        using var target = new RenderTargetBitmap(new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height));
        target.Render(window);
        target.Save(path, new PngBitmapEncoderOptions());
    }

    private static readonly Type[] Targets = [typeof(Button), typeof(TextBox), typeof(ComboBox), typeof(NumericUpDown), typeof(CheckBox), typeof(RadioButton), typeof(ToggleButton), typeof(Slider), typeof(ListBox), typeof(TabControl)];

    private static Rect Where(Control control, Visual window) => control.TransformToVisual(window) is { } matrix ? new Rect(default, control.Bounds.Size).TransformToAABB(matrix) : default;

    /// <summary>What is wrong with a window as it stands: a primary button that is not fully in view or sits in a scrolling area, a control that is outside the window or its scroll area with nothing to scroll, text cut without an ellipsis.</summary>
    private static IEnumerable<string> Audit(Window window)
    {
        var client = new Rect(default, window.Bounds.Size).Inflate(1);
        foreach (var visual in window.GetVisualDescendants().OfType<Control>())
        {
            if (!visual.IsEffectivelyVisible || visual.Bounds.Width < 1 || visual.Bounds.Height < 1) continue;
            var label = $"{visual.GetType().Name} '{visual.Name}'";
            var rect = Where(visual, window);
            if (visual is Button { IsDefault: true } || visual is Button button && button.Classes.Contains("primary") || visual is WrapPanel { Name: "DialogActions" })
            {
                if (!client.Contains(rect)) yield return $"{label} is not fully in the window ({rect} in {window.Bounds.Size})";
                if (visual.GetVisualAncestors().OfType<ScrollViewer>().Any()) yield return $"{label} sits inside a scrolling area";
            }

            if (Targets.Any(t => t.IsInstanceOfType(visual)))
            {
                var inside = client;
                var scrollers = visual.GetVisualAncestors().OfType<ScrollViewer>().ToList();
                foreach (var scroller in scrollers) inside = inside.Intersect(Where(scroller, window).Inflate(1));
                if (!inside.Contains(rect) && !scrollers.Any(s => CanScroll(s, rect, Where(s, window))))
                    yield return $"{label} is outside the window or its scroll area at {rect} and nothing scrolls to it";
            }

            if (visual is TextBlock { TextTrimming: var trimming, TextWrapping: Avalonia.Media.TextWrapping.NoWrap, Text: { Length: > 0 } text } block && trimming == Avalonia.Media.TextTrimming.None && !visual.GetVisualAncestors().OfType<ScrollViewer>().Any(s => s.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled))
            {
                var probe = new TextBlock { Text = text, FontSize = block.FontSize, FontFamily = block.FontFamily, FontWeight = block.FontWeight, FontStyle = block.FontStyle };
                probe.Measure(Size.Infinity);
                if (block.Bounds.Width + 1 < probe.DesiredSize.Width) yield return $"{label} '{text}' is cut: {block.Bounds.Width:0} of {probe.DesiredSize.Width:0} px";
            }
        }
    }

    // The scroll viewer can bring the rectangle into view when it has more content than room on the axis where the rectangle is cut and does not forbid scrolling there.
    private static bool CanScroll(ScrollViewer scroller, Rect rect, Rect viewport)
    {
        var verticalCut = rect.Top < viewport.Top - 1 || rect.Bottom > viewport.Bottom + 1;
        var horizontalCut = rect.Left < viewport.Left - 1 || rect.Right > viewport.Right + 1;
        return (!verticalCut || scroller.VerticalScrollBarVisibility != ScrollBarVisibility.Disabled && scroller.Extent.Height > scroller.Viewport.Height + 1)
            && (!horizontalCut || scroller.HorizontalScrollBarVisibility != ScrollBarVisibility.Disabled && scroller.Extent.Width > scroller.Viewport.Width + 1);
    }
}
