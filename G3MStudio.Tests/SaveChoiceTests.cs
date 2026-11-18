using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

public sealed class SaveChoiceTests
{
    private static async Task Settle(Func<bool> done)
    {
        for (var i = 0; i < 500 && !done(); i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        Assert.True(done(), "did not settle");
    }

    private static Task<(bool? Sync, bool Remember)?> Answer(bool? sync, bool remember, bool cancel = false) =>
        Task.FromResult<(bool? Sync, bool Remember)?>(cancel ? null : (sync, remember));

    private static async Task<MainWindow> OpenDirty(TempDir dir)
    {
        var window = TabsWorkspaceTests.NewWindow(dir);
        window.Workspaces.Projects.Registry.RememberRoot(Directory.CreateDirectory(dir.File("projects")).FullName);
        await window.OpenAsync(TestData.WriteSample(dir, objects: 2, withCode: true));
        window.Workspaces.Current!.Edit(new EditCodeCommand("gml_Script_probe", "return 2;"));
        return window;
    }

    [Fact]
    public async Task TheFirstSaveAsksAndTheBoxMakesTheNextOnesImmediate()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = await OpenDirty(dir);
            try
            {
                var asked = new List<bool>();
                window.SaveAsker = project => { asked.Add(project); return Answer(null, true); };
                window.Save();
                await Settle(() => !window.Workspaces.Current!.IsDirty);
                Assert.Equal([false], asked);
                Assert.Equal(PromptChoice.Yes, Prompts.Get(window.Workspaces.Settings.Current, Prompts.SaveFirst));

                window.Workspaces.Current!.Edit(new EditCodeCommand("gml_Script_probe", "return 3;"));
                window.Save();
                await Settle(() => !window.Workspaces.Current.IsDirty);
                Assert.Single(asked);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ARememberedAnswerSurvivesSwitchingFilesDuringTheQuestion()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = await OpenDirty(dir);
            try
            {
                var first = window.Workspaces.Current!;
                window.AskClose = _ => Task.FromResult(CloseChoice.DoItLater);
                window.SaveAsker = async _ =>
                {
                    await window.OpenAsync(TestData.WriteSample(dir, objects: 1, withCode: true, name: "other.win"));
                    return (null, true);
                };
                window.Save();
                await Settle(() => Prompts.Get(window.Workspaces.Settings.Current, Prompts.SaveFirst) == PromptChoice.Yes);
                Assert.False(ReferenceEquals(first, window.Workspaces.Current));
                Assert.True(first.IsDirty);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task CancelLeavesTheEditsUnsavedAndAsksAgainNextTime()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = await OpenDirty(dir);
            try
            {
                var asked = 0;
                window.SaveAsker = _ => { asked++; return Answer(null, true, cancel: true); };
                window.Save();
                await Settle(() => asked == 1);
                await Task.Delay(100);
                Assert.True(window.Workspaces.Current!.IsDirty);
                Assert.Equal(PromptChoice.Ask, Prompts.Get(window.Workspaces.Settings.Current, Prompts.SaveFirst));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ASecondSaveWhileTheQuestionIsOpenDoesNotAskAgainAndCloseDoesNotAsk()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = await OpenDirty(dir);
            try
            {
                var asked = 0;
                var gate = new TaskCompletionSource<(bool? Sync, bool Remember)?>();
                window.SaveAsker = _ => { asked++; return gate.Task; };
                window.Save();
                window.Save();
                Assert.Equal(1, asked);
                gate.SetResult((null, false));
                await Settle(() => !window.Workspaces.Current!.IsDirty);

                // The close prompt saves through the same code without the Save question.
                window.Workspaces.Current!.Edit(new EditCodeCommand("gml_Script_probe", "return 4;"));
                window.AskClose = _ => Task.FromResult(CloseChoice.Save);
                Assert.True(await window.ConfirmCloseAsync());
                Assert.False(window.Workspaces.Current.IsDirty);
                Assert.Equal(1, asked);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task InProjectModeProgressWritesOnlyTheProjectAndSyncAlsoWritesTheDataFile()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = await OpenDirty(dir);
            try
            {
                window.Workspaces.Current!.Edit(new EditCodeCommand("gml_Script_probe", "return 2;"));
                window.Notifications.Active.Single(n => n.ActionLabel == "Enable project").Action!();
                await Settle(() => window.Workspaces.Current!.Project is { IsBusy: false });
                var workspace = window.Workspaces.Current!;
                var link = workspace.Project!;

                workspace.Edit(new EditCodeCommand("gml_Script_probe", "return 5;"));
                var asked = new List<bool>();
                window.SaveAsker = project => { asked.Add(project); return Answer(false, true); };
                window.Save();
                await Settle(() => asked.Count == 1 && !link.IsBusy);
                await Task.Delay(200);
                Assert.True(link.NotSynced > 0, "progress must not write the data file");
                Assert.Equal(PromptChoice.No, Prompts.Get(window.Workspaces.Settings.Current, Prompts.SaveProject));

                window.Save(); // remembered: no question
                await Task.Delay(200);
                Assert.Single(asked);
                Assert.True(link.NotSynced > 0);

                window.Workspaces.Settings.Update(s => Prompts.Set(s, Prompts.SaveProject, PromptChoice.Yes));
                window.Save();
                await Settle(() => link.NotSynced == 0 && !link.IsBusy);
                Assert.Single(asked);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheDialogHasEnterAsDefaultEscapeAsCancelAndReportsTheBox()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var owner = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                foreach (var project in (bool[])[false, true])
                {
                    var before = owner.OwnedWindows.Count;
                    var answer = ProjectDialogs.AskSaveAsync(owner, project);
                    var dialog = owner.OwnedWindows[before];
                    await Settle(() => dialog.IsVisible);
                    var buttons = dialog.GetVisualDescendants().OfType<Button>().ToList();
                    var main = buttons.Single(b => b.Name == (project ? "SaveSync" : "Save"));
                    Assert.True(main.IsDefault);
                    Assert.True(buttons.Single(b => b.Name == "Cancel").IsCancel);
                    Assert.Equal(project, buttons.Any(b => b.Name == "SaveProgress"));
                    Assert.Equal(Loc.Current[project ? "save.remember" : "prompts.dontAsk"], dialog.GetVisualDescendants().OfType<CheckBox>().Single().Content);
                    dialog.GetVisualDescendants().OfType<CheckBox>().Single().IsChecked = true;
                    main.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var result = await answer;
                    Assert.Equal((project ? true : null, true), (result!.Value.Sync, result.Value.Remember));

                    answer = ProjectDialogs.AskSaveAsync(owner, project);
                    dialog = owner.OwnedWindows[before];
                    await Settle(() => dialog.IsVisible);
                    dialog.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "Cancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Assert.Null(await answer);
                }
            }
            finally { owner.Close(); }
        });
    }

    [Fact]
    public void TheSavePromptsAreListedAndOnlyTheProjectOneRemembersNo()
    {
        var s = new AppSettings();
        Assert.Equal(s, Prompts.Set(s, Prompts.SaveFirst, PromptChoice.No));
        Assert.Equal(PromptChoice.Yes, Prompts.Get(Prompts.Set(s, Prompts.SaveFirst, PromptChoice.Yes), Prompts.SaveFirst));
        Assert.Equal(PromptChoice.No, Prompts.Get(Prompts.Set(s, Prompts.SaveProject, PromptChoice.No), Prompts.SaveProject));
        Assert.Equal(PromptChoice.Ask, Prompts.Get(Prompts.ResetAll(Prompts.Set(s, Prompts.SaveProject, PromptChoice.Yes)), Prompts.SaveProject));
    }
}
