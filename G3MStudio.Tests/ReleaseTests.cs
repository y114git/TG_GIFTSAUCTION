#pragma warning disable CA2000, CA1849
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MStudio.Editor;
using G3MStudio.Rooms;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>Closed windows and disposed workspaces must be collectable, or every test (and every user session that opens files) keeps them alive through a static event.</summary>
public sealed class ReleaseTests
{
    private const int Tries = 8;

    private static async Task AssertCollected(params WeakReference[] refs)
    {
        for (var i = 0; i < Tries; i++)
        {
            Dispatcher.UIThread.RunJobs();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            if (refs.All(r => !r.IsAlive)) return;
            await Task.Delay(20, TestContext.Current.CancellationToken);
        }

        Assert.Fail("still alive: " + string.Join(", ", refs.Where(r => r.IsAlive).Select(r => r.Target?.GetType().Name)));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference EmptyWindow()
    {
        Loc.Current.SetLanguage("en");
        var window = new MainWindow();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Close();
        return new WeakReference(window);
    }

    [Fact]
    public async Task AClosedEmptyMainWindowIsCollected()
    {
        await HeadlessApp.Run(async () => await AssertCollected(EmptyWindow()));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference[]> WindowWithFile(string path, string userData)
    {
        Loc.Current.SetLanguage("en");
        var data = new UserData(userData);
        var window = new MainWindow(new WorkspaceService(data, RecentFiles.Load(data)));
        window.Show();
        await window.OpenAsync(path);
        Dispatcher.UIThread.RunJobs();
        var workspace = window.Workspaces.Current!;
        var explorer = window.Explorer.Model;
        var dock = window.Docking;
        window.Docking.OpenTab("resource", ResourceTypes.KeyOf("scripts", workspace.Data.Scripts.First().Name.Content!), false);
        Dispatcher.UIThread.RunJobs();
        window.Workspaces.Close();
        window.Close();
        return [new(window), new(workspace), new(explorer), new(dock)];
    }

    [Fact]
    public async Task AClosedMainWindowWithAFileAndATabIsCollectedWithItsWorkspace()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        await HeadlessApp.Run(async () => await AssertCollected(await WindowWithFile(path, dir.File("user"))));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference[]> CodeTab(string path, string userData)
    {
        var data = new UserData(userData);
        var workspace = await new WorkspaceService(data, RecentFiles.Load(data)).OpenAsync(path, null, CancellationToken.None);
        var tab = new CodeEditorTab(workspace, "gml_Script_probe");
        var window = new Window { Content = tab, Width = 900, Height = 600 };
        window.Show();
        await tab.Ready;
        window.Close();
        workspace.Dispose();
        return [new(window), new(tab), new(workspace)];
    }

    [Fact]
    public async Task AClosedCodeEditorTabIsCollected()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, withCode: true);
        await HeadlessApp.Run(async () => await AssertCollected(await CodeTab(path, dir.File("user"))));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] SettingsWindow(TempDir dir)
    {
        var window = TabsWorkspaceTests.NewWindow(dir);
        window.Registry.Execute("settings.open");
        Dispatcher.UIThread.RunJobs();
        window.Close();
        return [new(window)];
    }

    [Fact]
    public async Task AClosedMainWindowWithTheSettingsPageOpenIsCollected()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () => await AssertCollected(SettingsWindow(dir)));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] ImageEditor()
    {
        var view = new ImageEditorView(new LocalImageSession(new PixelBuffer(64, 32)));
        var window = new Window { Width = 1200, Height = 700, Content = view };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Close();
        view.Dispose();
        return [new(window), new(view)];
    }

    [Fact]
    public async Task AClosedImageEditorIsCollected()
    {
        await HeadlessApp.Run(async () => await AssertCollected(ImageEditor()));
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static async Task<WeakReference[]> RoomPreviewOf(string path, string userData)
    {
        var data = new UserData(userData);
        var workspace = await new WorkspaceService(data, RecentFiles.Load(data)).OpenAsync(path, cancellationToken: CancellationToken.None);
        var room = new GameMakerRoom { Name = workspace.Data.Strings.MakeString("rm_k", out _), Layers = [] };
        workspace.Data.Rooms.Add(room);
        var textures = new TextureBitmaps();
        var preview = new RoomPreview(room, textures, workspace);
        var window = new Window { Content = preview, Width = 900, Height = 600 };
        window.Show();
        await preview.Ready;
        window.Close();
        textures.Dispose();
        workspace.Dispose();
        return [new(window), new(preview), new(workspace)];
    }

    [Fact]
    public async Task AClosedRoomPreviewIsCollected()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 1);
        await HeadlessApp.Run(async () => await AssertCollected(await RoomPreviewOf(path, dir.File("user"))));
    }
}
