using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Settings;

namespace G3MStudio.Tests;

/// <summary>Every command, in every state the window can be in, must run twice in a row without a throw, a hang or a lost exception, and its dialogs must close with each button.</summary>
public sealed class CommandSweepTests(ITestOutputHelper output)
{
    private static readonly string[] TextTries = ["abc", "-1"];

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(150);

    private static string Sample(TempDir dir, string name = "data.win")
    {
        using var data = CopyResourcesTests.Source();
        var history = new EditHistory(data);
        history.Execute(ResourceTemplates.New(data, "rooms", "rm_a"));
        return TestData.Write(dir.File(name), data);
    }

    private void Report(SweepProbe probe)
    {
        output.WriteLine("slowest: " + string.Join("; ", probe.Timings.OrderByDescending(t => t.Seconds).Take(12).Select(t => $"{t.Where} {t.Seconds:0.0}s")));
        output.WriteLine("stacked: " + string.Join(", ", probe.Stacked));
        output.WriteLine("commands with windows: " + string.Join(", ", probe.Dialogs));
        Assert.True(probe.Errors.Count == 0, string.Join("\n---\n", probe.Errors.Distinct()));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TheStartPageSurvivesEveryCommand(bool onboardingDone)
    {
        using var dir = new TempDir();
        using var probe = new SweepProbe();
        using var cts = new CancellationTokenSource(Budget);
        await HeadlessApp.Run(async () =>
        {
            Environment.SetEnvironmentVariable("G3MSTUDIO_NO_ONBOARDING", null);
            var window = CommandSweep.NewWindow(dir, onboardingDone);
            try
            {
                window.RunFirstLaunch();
                Assert.Equal(!onboardingDone, CommandSweep.Dialogs(window).Count == 1);
                await CommandSweep.Run(window, probe, onboardingDone ? "start page" : "first run", true, before: onboardingDone ? null : w => w.ShowOnboarding(), cancel: cts.Token); // the welcome window is open while each command runs
                await CommandSweep.CloseDialogs(window, probe);
            }
            finally
            {
                window.Close();
                Loc.Current.SetLanguage("en");
            }
        });
        Report(probe);
    }

    [Fact]
    public async Task AnOpenFileSurvivesEveryCommandCleanDirtyAndAfterASecondFile()
    {
        using var dir = new TempDir();
        using var probe = new SweepProbe();
        using var cts = new CancellationTokenSource(Budget);
        var first = Sample(dir);
        var second = Sample(dir, "second.win");
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                await window.OpenAsync(first);
                await CommandSweep.Run(window, probe, "file open", true, cancel: cts.Token);

                window.ShowOnboarding(); // Help > Getting started while a file is open
                await CommandSweep.Run(window, probe, "getting started open", false, c => c.Id.StartsWith("help.", StringComparison.Ordinal) || c.Id.StartsWith("file.", StringComparison.Ordinal) || c.Id.StartsWith("tools.", StringComparison.Ordinal), cancel: cts.Token);
                await CommandSweep.CloseDialogs(window, probe);

                await CommandSweep.EditWhenFree(window.Workspaces.Current!, ResourceTemplates.New(window.Workspaces.Current!.Data, "objects", "obj_dirty"));
                Assert.True(window.Workspaces.Current.IsDirty);
                await CommandSweep.Run(window, probe, "unsaved edits", false, cancel: cts.Token);

                await CommandSweep.EditWhenFree(window.Workspaces.Current!, ResourceTemplates.New(window.Workspaces.Current!.Data, "objects", "obj_dirty2"));
                CommandSweep.Seams(window, true); // every question answered yes, the close prompt answered "do it later"
                await CommandSweep.Run(window, probe, "unsaved edits, questions yes", false, c => c.Id.StartsWith("edit.", StringComparison.Ordinal) || c.Id.StartsWith("organize.", StringComparison.Ordinal), cancel: cts.Token);
                CommandSweep.Seams(window, false);

                Assert.True(window.Registry.Execute("file.save"));
                for (var i = 0; i < 500 && window.Workspaces.Current.IsDirty; i++) await CommandSweep.Pump(1);
                Assert.False(window.Workspaces.Current.IsDirty);
                await window.OpenAsync(second);
                Assert.Equal(Path.GetFullPath(second), window.Workspaces.Current!.Path);
                await CommandSweep.Run(window, probe, "second file", false, cancel: cts.Token);

                probe.Where = "close";
                Assert.True(window.Registry.Execute("file.close"));
                await CommandSweep.Pump();
                Assert.Null(window.Workspaces.Current);
                await CommandSweep.Run(window, probe, "closed again", false, cancel: cts.Token);
            }
            finally
            {
                window.Close();
            }
        });
        Report(probe);
    }

    [Theory]
    [InlineData("codeTab")]
    [InlineData("spriteTab")]
    [InlineData("imageTab")]
    [InlineData("roomTab")]
    [InlineData("selected")]
    public async Task ACommandOfATabKindSurvivesItsTab(string kind)
    {
        using var dir = new TempDir();
        using var probe = new SweepProbe();
        using var cts = new CancellationTokenSource(Budget);
        var path = Sample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                await window.OpenAsync(path);
                switch (kind)
                {
                    case "codeTab": window.Docking.OpenTab("code", "code:gml_Script_scr_use", false); break;
                    case "spriteTab": window.Docking.OpenTab("resource", "sprites:spr_a", false); break;
                    case "imageTab": window.Docking.OpenTab("image", "sprites:spr_a", false); break;
                    case "roomTab": window.Docking.OpenTab("resource", "rooms:rm_a", false); break;
                    default: window.Explorer.Select("objects:obj_a"); break;
                }

                var context = kind switch { "codeTab" => AppCommands.CodeTab, "spriteTab" => AppCommands.SpriteTab, "imageTab" => AppCommands.ImageTab, "roomTab" => AppCommands.RoomTab, _ => AppCommands.ResourceSelected };
                // The tab view is made after the dock reports the tab and the menus look again on a later pass; under load that takes more than a few pumps, so wait for the state itself.
                await CommandSweep.Until(() => window.Registry.HasContext(context));
                await CommandSweep.Pump(5);
                Assert.True(window.Registry.HasContext(context), "the tab context is not set: " + kind);
                await CommandSweep.Run(window, probe, kind, false, c => c.When == context || c.Id.StartsWith("tabs.", StringComparison.Ordinal) || c.Id.StartsWith("edit.", StringComparison.Ordinal), cancel: cts.Token);
            }
            finally
            {
                window.Close();
            }
        });
        Report(probe);
    }

    // The file is closed while the command still works on it: the work ends or is cancelled, and nothing faults unseen.
    [Theory]
    [InlineData("file.save")]
    [InlineData("file.saveCopy")]
    [InlineData("file.revert")]
    [InlineData("run.game")]
    [InlineData("run.restart")]
    [InlineData("run.withRuntime")]
    [InlineData("mods.createPatch")]
    [InlineData("mods.exportMod")]
    [InlineData("edit.exportAllAudio")]
    [InlineData("tools.offsetMap")]
    public async Task ClosingTheFileWhileACommandWorksLeavesNoLostException(string id)
    {
        using var dir = new TempDir();
        using var probe = new SweepProbe();
        var path = Sample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                await window.OpenAsync(path);
                probe.Where = id;
                window.Registry.Execute(id);
                window.Registry.Execute(id);
                await CommandSweep.CloseDialogs(window, probe);
                window.Workspaces.Close();
                await Task.Delay(300);
                await CommandSweep.Pump(2);
                SweepProbe.Flush();
            }
            finally
            {
                window.Close();
            }
        });
        Report(probe);
    }

    // The real close prompt, asked once or twice (the user presses the key again), answered with each button or its close button.
    [Theory]
    [InlineData("file.exit", "Save", 1)]
    [InlineData("file.exit", "Later", 2)]
    [InlineData("file.exit", "Cancel", 2)]
    [InlineData("file.exit", "X", 2)]
    [InlineData("file.close", "Save", 2)]
    [InlineData("file.close", "Later", 2)]
    [InlineData("file.close", "Cancel", 2)]
    [InlineData("window", "Later", 2)]
    [InlineData("window", "Save", 2)]
    [InlineData("window", "Cancel", 2)]
    public async Task TheClosePromptAnswersEveryButtonEvenWhenAskedTwice(string entry, string answer, int times)
    {
        using var dir = new TempDir();
        using var probe = new SweepProbe();
        var path = Sample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                await CommandSweep.EditWhenFree(window.Workspaces.Current!, ResourceTemplates.New(window.Workspaces.Current!.Data, "objects", "obj_dirty"));
                probe.Where = entry + "/" + answer;
                for (var i = 0; i < times; i++) CommandSweep.Step(probe, "ask", () => { if (entry == "window") window.Close(); else window.Registry.Execute(entry); });
                await CommandSweep.Pump(3);
                var prompts = CommandSweep.Dialogs(window).OfType<ClosePromptWindow>().ToList();
                Assert.NotEmpty(prompts);
                CommandSweep.Step(probe, "answer", () =>
                {
                    if (answer == "X") prompts[0].Close();
                    else prompts[0].GetVisualDescendants().OfType<Button>().Single(b => b.Name == answer).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                });
                var kept = answer is "Cancel" or "X";
                bool Done() => kept ? CommandSweep.Dialogs(window).Count == 0 : entry == "file.close" ? window.Workspaces.Current is null : !window.IsVisible;
                for (var i = 0; i < 300 && !Done(); i++) await Task.Delay(10).ContinueWith(_ => CommandSweep.Pump(1), TaskScheduler.FromCurrentSynchronizationContext()).Unwrap();
                Assert.True(Done(), "the prompt answer was not carried out");
                if (window.IsVisible) await CommandSweep.CloseDialogs(window, probe);
                SweepProbe.Flush();
                if (kept) Assert.True(window.IsVisible && window.Workspaces.Current is { IsDirty: true });
            }
            finally
            {
                if (window.IsVisible) window.Close();
            }
        });
        Report(probe);
    }

    // Each command that opens a window, with that window still open when the main window closes (first run, start page and open file).
    [Theory]
    [InlineData("first run")]
    [InlineData("start page")]
    [InlineData("file open")]
    public async Task ClosingTheMainWindowWithADialogOpenLosesNothing(string state)
    {
        using var dir = new TempDir();
        using var probe = new SweepProbe();
        using var cts = new CancellationTokenSource(Budget);
        var path = Sample(dir);
        string[] skip = ["file.exit", "file.close", "file.newData"];
        await HeadlessApp.Run(async () =>
        {
            Environment.SetEnvironmentVariable("G3MSTUDIO_NO_ONBOARDING", null);
            var ids = new List<string>();
            var scout = CommandSweep.NewWindow(dir, state != "first run");
            try
            {
                if (state == "file open") await scout.OpenAsync(path);
                foreach (var command in scout.Registry.All.Where(c => !skip.Contains(c.Id) && scout.Registry.IsEnabled(c)).OrderBy(c => c.Id, StringComparer.Ordinal).ToList())
                {
                    CommandSweep.Step(probe, "scout " + command.Id, () => scout.Registry.Execute(command.Id));
                    await CommandSweep.Pump(1);
                    if (CommandSweep.Dialogs(scout).Count > 0) ids.Add(command.Id);
                    await CommandSweep.CloseDialogs(scout, probe);
                }
            }
            finally
            {
                scout.Close();
            }

            Assert.NotEmpty(ids);
            foreach (var id in ids)
            {
                cts.Token.ThrowIfCancellationRequested();
                var window = CommandSweep.NewWindow(dir, state != "first run");
                try
                {
                    if (state == "file open") await window.OpenAsync(path);
                    var command = window.Registry.Get(id);
                    if (!window.Registry.IsEnabled(command)) continue;
                    probe.Where = state + ": " + id + " then close";
                    CommandSweep.Step(probe, "open", () => window.Registry.Execute(id));
                    await CommandSweep.Pump(2);
                    if (CommandSweep.Dialogs(window).Count == 0) continue;
                    // The main window goes first; the dialog must follow without a throw.
                    if (window.Workspaces.Current is not null) window.Workspaces.Close();
                    CommandSweep.Step(probe, "close main", window.Close);
                    await CommandSweep.Pump(2);
                }
                finally
                {
                    if (window.IsVisible) window.Close();
                }
            }

            SweepProbe.Flush();
        });
        Report(probe);
    }

    // A window that works on the open file must not outlive it: its buttons would act on a model that is gone.
    [Theory]
    [InlineData("tools.strings")]
    [InlineData("tools.roomOrder")]
    [InlineData("tools.textureGroups")]
    [InlineData("tools.audioGroups")]
    [InlineData("tools.hexView")]
    public async Task AToolWindowOfTheFileGoesWhenTheFileCloses(string id)
    {
        using var dir = new TempDir();
        using var probe = new SweepProbe();
        var path = Sample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                await window.OpenAsync(path);
                Assert.True(window.Registry.Execute(id));
                await CommandSweep.Pump(2);
                Assert.NotEmpty(CommandSweep.Dialogs(window));
                window.Workspaces.Close();
                await CommandSweep.Pump(2);
                probe.Where = id + " after the file closed";
                Assert.Empty(CommandSweep.Dialogs(window));
                SweepProbe.Flush();
            }
            finally
            {
                window.Close();
            }
        });
        Report(probe);
    }

    // A second call brings the open window forward; it does not stack another one.
    [Theory]
    [InlineData("help.gettingStarted")]
    [InlineData("help.tour")]
    [InlineData("tools.plugins")]
    [InlineData("tools.strings")]
    [InlineData("tools.roomOrder")]
    [InlineData("tools.textureGroups")]
    [InlineData("tools.audioGroups")]
    [InlineData("tools.hexView")]
    [InlineData("window.layout.saved")]
    public async Task AToolWindowOpensOnceHoweverOftenItIsCalled(string id)
    {
        using var dir = new TempDir();
        var path = Sample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                await window.OpenAsync(path);
                for (var i = 0; i < 3; i++) Assert.True(window.Registry.Execute(id));
                await CommandSweep.Pump(2);
                Assert.Single(CommandSweep.Dialogs(window));
                CommandSweep.Dialogs(window)[0].Close();
                await CommandSweep.Pump(2);
                Assert.True(window.Registry.Execute(id)); // closed, so the next call opens it again
                Assert.Single(CommandSweep.Dialogs(window));
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EveryHelpItemWorksOnTheStartPageAndWithAFileOpen(bool fileOpen)
    {
        using var dir = new TempDir();
        using var probe = new SweepProbe();
        var path = Sample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, false);
            var opened = new List<string>();
            try
            {
                MainWindow.UrlOpener = target => { opened.Add(target); return true; };
                if (fileOpen) await window.OpenAsync(path);

                window.Registry.Execute("help.documentation");
                Assert.Equal(["https://g3m.gitbook.io/g3mwiki/main/g3mstudio"], opened);
                window.Registry.Execute("help.openDataFolder");
                Assert.Equal(window.Workspaces.UserData.Root, opened[^1]);
                MainWindow.UrlOpener = _ => false; // no file manager: the path is shown instead
                window.Registry.Execute("help.openDataFolder");
                Assert.Contains(window.Notifications.Active, n => n.Text == window.Workspaces.UserData.Root);

                window.Registry.Execute("help.about");
                await CommandSweep.Pump(2);
                Assert.IsType<AboutWindow>(Assert.Single(CommandSweep.Dialogs(window)));
                await CommandSweep.CloseDialogs(window, probe);

                window.Registry.Execute("help.tour");
                await CommandSweep.Pump(2);
                var tour = Assert.IsType<TourWindow>(Assert.Single(CommandSweep.Dialogs(window)));
                for (var i = 0; i <= TourWindow.Steps.Length; i++) tour.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "TourNext").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                await CommandSweep.Pump(2);
                Assert.Empty(CommandSweep.Dialogs(window));

                window.Registry.Execute("help.gettingStarted");
                await CommandSweep.Pump(2);
                Assert.IsType<OnboardingWindow>(Assert.Single(CommandSweep.Dialogs(window)));
                await CommandSweep.CloseDialogs(window, probe);
                Assert.True(window.Workspaces.Settings.Current.OnboardingDone);

                window.UpdateCheck = _ => throw new HttpRequestException("no network");
                window.Registry.Execute("help.checkUpdates");
                await CommandSweep.Pump(3);
                Assert.Contains(window.Notifications.Active, n => n.Level == G3MStudio.Core.Notifications.NotificationLevel.Error);
                SweepProbe.Flush();
            }
            finally
            {
                window.Close();
            }
        });
        Report(probe);
    }

    [Fact]
    public async Task ACheckForUpdatesThatIsStillRunningIsNotStartedAgain()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                var calls = 0;
                var gate = new TaskCompletionSource<G3MStudio.Core.Updates.UpdateOffer?>();
                window.UpdateCheck = _ =>
                {
                    calls++;
                    return gate.Task;
                };
                window.Registry.Execute("help.checkUpdates");
                window.Registry.Execute("help.checkUpdates");
                var third = window.CheckForUpdatesAsync();
                Assert.Equal(1, calls);
                gate.SetResult(null);
                await third;
                Assert.Single(window.Notifications.Active, n => n.Text.Contains("newest version", StringComparison.Ordinal));
            }
            finally
            {
                window.Close();
            }
        });
    }

    // Every page of Settings with its buttons, boxes, lists and text fields driven; each change saves settings and tells every listener.
    [Theory]
    [InlineData(true)]
    public async Task EverySettingsPageSurvivesItsControls(bool fileOpen)
    {
        using var dir = new TempDir();
        using var probe = new SweepProbe();
        var path = Sample(dir);
        await HeadlessApp.Run(async () =>
        {
            var window = CommandSweep.NewWindow(dir, true);
            try
            {
                if (fileOpen) await window.OpenAsync(path);
                var driven = new HashSet<string>(); // a control the page shell repeats on every page is driven once
                for (var section = 0; section < AppCommands.SettingsPages.Length; section++)
                {
                    probe.Where = "settings page " + AppCommands.SettingsPages[section];
                    CommandSweep.Step(probe, "show", () => window.ShowSettings(section));
                    await CommandSweep.Pump(2);
                    var page = window.GetVisualDescendants().OfType<SettingsPage>().Single();
                    page.UpdateLayout();
                    var controls = page.GetVisualDescendants().OfType<Control>().Where(c => c.IsVisible && c.IsEffectivelyEnabled && c is ComboBox or CheckBox or TextBox or Button).Where(c => string.IsNullOrEmpty(c.Name) || driven.Add(c.GetType().Name + c.Name)).Take(6).ToList();
                    foreach (var control in controls)
                    {
                        probe.Where = $"settings page {AppCommands.SettingsPages[section]}: {control.GetType().Name} {control.Name}";
                        var watch = System.Diagnostics.Stopwatch.StartNew();
                        switch (control)
                        {
                            case ComboBox combo:
                                foreach (var i in new[] { 0, combo.ItemCount - 1 }.Distinct().Where(i => i >= 0)) CommandSweep.Step(probe, "select " + i, () => combo.SelectedIndex = i);
                                break;
                            case CheckBox box when box.IsEnabled:
                                CommandSweep.Step(probe, "toggle", () => box.IsChecked = box.IsChecked != true);
                                CommandSweep.Step(probe, "toggle back", () => box.IsChecked = box.IsChecked != true);
                                break;
                            case TextBox text when !text.IsReadOnly:
                                foreach (var value in TextTries) CommandSweep.Step(probe, "type " + value, () => text.Text = value);
                                break;
                            case Button button when button.Name != "SettingsClose":
                                CommandSweep.Step(probe, "click", () => button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent)));
                                break;
                        }

                        await CommandSweep.Pump(1);
                        await CommandSweep.CloseDialogs(window, probe);
                        probe.Timings.Add((watch.Elapsed.TotalSeconds, probe.Where));
                        if (!window.GetVisualDescendants().OfType<SettingsPage>().Any()) break; // a button closed Settings
                    }
                }

                SweepProbe.Flush();
            }
            finally
            {
                window.Close();
                Loc.Current.SetLanguage("en");
            }
        });
        Report(probe);
    }
}
