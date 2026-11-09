using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Notifications;
using G3MStudio.Panels;

namespace G3MStudio.Tests;

public sealed class UxReviewTests
{
    private static KeyEventArgs Press(Key key) => new() { RoutedEvent = InputElement.KeyDownEvent, Key = key };

    private static T Named<T>(MainWindow window, string name) where T : Control
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    }

    [Fact]
    public async Task EscapeClosesADialogWithItsCancelResult()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var prompt = new ClosePromptWindow(new ClosePrompt("probe", 3, false));
                var asked = prompt.AskAsync(window);
                prompt.RaiseEvent(Press(Key.Escape));
                Assert.Equal(CloseChoice.Cancel, await asked);

                var about = new AboutWindow();
                about.Show(window);
                about.RaiseEvent(Press(Key.Escape));
                Assert.False(about.IsVisible);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ARowAnnouncesItsCountOnlyWhenItShowsOne()
    {
        var header = new ExplorerRow(ExplorerRowKind.Type, "objects", "Objects", 0) { Count = 4 };
        Assert.Equal("Objects, 4", header.AutomationName);
        Assert.Equal("Objects", (header with { ShowCount = false }).AutomationName);
        Assert.Equal("obj_a", new ExplorerRow(ExplorerRowKind.Item, "objects", "obj_a", 1).AutomationName);
    }

    [Fact]
    public async Task AToastStaysWhileThePointerIsOverItAndNamesItsLevel()
    {
        await HeadlessApp.Run(() =>
        {
            Loc.Current.SetLanguage("en");
            var center = new NotificationCenter();
            var host = new ToastHost();
            host.Bind(center);
            var item = center.Post(NotificationLevel.Info, "saved");
            var toast = Assert.IsType<Border>(Assert.Single(host.Children));
            Assert.Equal(Avalonia.Automation.AutomationLiveSetting.Polite, Avalonia.Automation.AutomationProperties.GetLiveSetting(toast));
            Assert.Equal("Info: saved", Avalonia.Automation.AutomationProperties.GetName(toast));

            var later = DateTimeOffset.Now.AddMinutes(1);
            toast.RaiseEvent(new PointerEventArgs(InputElement.PointerEnteredEvent, toast, new Pointer(0, PointerType.Mouse, true), toast, default, 0, new PointerPointProperties(), KeyModifiers.None));
            host.Tick(later);
            Assert.Contains(center.Active, n => n.Id == item.Id);

            toast.RaiseEvent(new PointerEventArgs(InputElement.PointerExitedEvent, toast, new Pointer(0, PointerType.Mouse, true), toast, default, 0, new PointerPointProperties(), KeyModifiers.None));
            host.Tick(later);
            Assert.Empty(center.Active);
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task EnterOpensTheSelectedProblem()
    {
        await HeadlessApp.Run(() =>
        {
            var model = new ProblemsModel();
            var diagnostic = new G3MLib.Analyzer.Compiler.GmlDiagnostic(G3MLib.Analyzer.Compiler.DiagnosticSeverity.Error, "GML2001", "Expected ')'", new G3MLib.Analyzer.Compiler.SourceSpan(0, 1, 1, 8, 1, 9));
            model.Set("gml_Script_probe", [diagnostic]);
            var panel = new ProblemsPanel();
            panel.Bind(model);
            var window = new Window { Content = panel };
            window.Show();
            try
            {
                var list = panel.GetVisualDescendants().OfType<ListBox>().Single();
                Problem? opened = null;
                panel.OpenRequested += p => opened = p;
                list.RaiseEvent(Press(Key.Enter));
                Assert.Null(opened); // nothing selected
                list.SelectedIndex = 0;
                list.RaiseEvent(Press(Key.Enter));
                Assert.Equal(8, opened?.Column);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task SettingsSearchSaysWhenNothingMatchesAndEnterFocusesTheFirstSetting()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Registry.Execute("settings.open");
                var search = Named<TextBox>(window, "SettingsSearch");
                var nothing = Named<StackPanel>(window, "SettingsNothing");
                Assert.False(nothing.IsVisible);
                search.Text = "zzzz-no-such-setting";
                Dispatcher.UIThread.RunJobs();
                Assert.True(nothing.IsVisible);
                Assert.Equal(-1, Named<ListBox>(window, "SettingsNav").SelectedIndex);

                search.Text = Loc.Current["settings.backups"];
                Dispatcher.UIThread.RunJobs();
                Assert.False(nothing.IsVisible);
                search.Focus();
                search.RaiseEvent(Press(Key.Enter));
                var focused = (TopLevel.GetTopLevel(window)?.FocusManager?.GetFocusedElement() as Avalonia.Visual)!;
                Assert.Contains(focused.GetSelfAndVisualAncestors(), v => v is Control { Name: "Backups" });
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }

    [Fact]
    public async Task TheDropBoxOpensTheFilePickerWhenClicked()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var opened = 0;
                var start = window.Start;
                start.Browse += () => opened++;
                var box = Named<Border>(window, "DropBox");
                Assert.True(box.Focusable);
                box.RaiseEvent(Press(Key.Enter));
                Assert.Equal(1, opened);
                var center = Avalonia.VisualExtensions.TranslatePoint(box, new Avalonia.Point(box.Bounds.Width / 2, box.Bounds.Height / 2), window)!.Value;
                Avalonia.Headless.HeadlessWindowExtensions.MouseDown(window, center, Avalonia.Input.MouseButton.Left);
                Avalonia.Headless.HeadlessWindowExtensions.MouseUp(window, center, Avalonia.Input.MouseButton.Left);
                Assert.Equal(2, opened);

                var settings = 0;
                start.Settings += () => settings++;
                Named<Button>(window, "SettingsButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Assert.Equal(1, settings);
            }
            finally { window.Close(); }
            return Task.CompletedTask;
        });
    }
}
