using Avalonia;
using G3MLib.DataFile;
using Avalonia.Controls;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>Renders the window with a real game open into PNG files, so the look of the editors can be checked by eye. Runs only when G3MSTUDIO_SHOTS names a folder.</summary>
public sealed class ScreenshotTests
{
    [Fact]
    public async Task TheMainEditorsRenderToPictures()
    {
        var folder = Environment.GetEnvironmentVariable("G3MSTUDIO_SHOTS");
        var corpus = Environment.GetEnvironmentVariable("G3MSTUDIO_CORPUS_DIR");
        Assert.SkipWhen(string.IsNullOrEmpty(folder) || string.IsNullOrEmpty(corpus), "G3MSTUDIO_SHOTS or G3MSTUDIO_CORPUS_DIR is not set");
        Directory.CreateDirectory(folder);
        // The first file, smallest first, that has the resource types worth a picture.
        string? file = null;
        foreach (var candidate in Directory.EnumerateFiles(corpus, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length))
        {
            using var probe = await G3MLib.DataFile.GameMakerIO.ReadAsync(candidate, cancellationToken: TestContext.Current.CancellationToken);
            if (probe.Fonts?.Count > 0 && probe.Paths?.Count > 0 && probe.Rooms.Count > 0) { file = candidate; break; }
        }

        file ??= Directory.EnumerateFiles(corpus, "data.win", SearchOption.AllDirectories).OrderBy(f => new FileInfo(f).Length).First();
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.Width = 1500;
                window.Height = 950;
                await Task.Delay(300);
                Dispatcher.UIThread.RunJobs();
                Shoot(window, Path.Combine(folder, "00-start.png"));
                await window.OpenAsync(file);
                Shoot(window, Path.Combine(folder, "01-opened.png"));

                var workspace = window.Workspaces.Current!;
                string[] wanted = ["rooms", "sprites", "objects", "sounds", "fonts", "paths", "scripts", "particleSystems", "sequences", "animationCurves", "shaders"];
                foreach (var type in wanted)
                {
                    var item = type switch
                    {
                        "rooms" => workspace.Data.Rooms.OrderByDescending(r => RoomInstanceCommand.Lists(r).Sum(l => l.Count)).FirstOrDefault()?.Name.Content,
                        "sprites" => workspace.Data.Sprites.FirstOrDefault(s => s.Textures.Count > 1)?.Name.Content,
                        "objects" => workspace.Data.GameObjects.FirstOrDefault(o => o.Events.Any(e => e.Count > 0))?.Name.Content,
                        "sounds" => workspace.Data.Sounds.FirstOrDefault()?.Name.Content,
                        "fonts" => workspace.Data.Fonts.FirstOrDefault()?.Name.Content,
                        "paths" => workspace.Data.Paths.FirstOrDefault()?.Name.Content,
                        "particleSystems" => workspace.Data.ParticleSystems?.FirstOrDefault(p => p.Emitters?.Count > 0)?.Name.Content,
                        "sequences" => workspace.Data.Sequences?.FirstOrDefault(q => q.Tracks?.Count > 0)?.Name.Content,
                        "animationCurves" => workspace.Data.AnimationCurves?.FirstOrDefault()?.Name.Content,
                        "shaders" => workspace.Data.Shaders?.FirstOrDefault()?.Name.Content,
                        _ => workspace.Data.Scripts.FirstOrDefault()?.Name.Content,
                    };
                    if (item is null) continue;
                    window.Docking.OpenTab("resource", ResourceTypes.KeyOf(type, item), false);
                    window.Explorer.Select(ResourceTypes.KeyOf(type, item));
                    for (var i = 0; i < 6; i++)
                    {
                        await Task.Delay(150);
                        Dispatcher.UIThread.RunJobs();
                    }

                    if (type == "rooms" && Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Editor.RoomPreview>().FirstOrDefault() is { } roomView)
                    {
                        roomView.View.Tool = Rooms.RoomTool.Place;
                        window.Explorer.Select(ResourceTypes.KeyOf("objects", workspace.Data.GameObjects[0].Name.Content!));
                        await Task.Delay(300);
                        Dispatcher.UIThread.RunJobs();
                    }

                    // A particle preview starts empty and steps with a timer; step it so the picture shows particles.
                    if (type == "particleSystems" && Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Editor.ParticlePreviewView>().FirstOrDefault() is { } preview)
                    {
                        for (var i = 0; i < 40; i++) preview.Advance();
                        Dispatcher.UIThread.RunJobs();
                    }

                    Shoot(window, Path.Combine(folder, $"02-{type}.png"));
                }

                // The corpus has no timeline with moments, so one is built in memory (not through the undo history) to see the tab.
                using var shotTimeline = new G3MLib.DataFile.Models.GameMakerTimeline { Name = workspace.Data.Strings.MakeString("tl_screenshot", out _) };
                workspace.Data.Timelines.Add(shotTimeline);
                var firstCode = workspace.Data.Code.First(c => c.Name.Content is not null).Name.Content ?? "";
                new TimelineMomentCommand("tl_screenshot", TimelineMomentCommand.Add, -1, 30).Apply(workspace.Data);
                new TimelineMomentCommand("tl_screenshot", TimelineMomentCommand.Add, -1, 90).Apply(workspace.Data);
                new TimelineActionCommand("tl_screenshot", TimelineActionCommand.Add, 0, -1, firstCode).Apply(workspace.Data);
                window.Docking.OpenTab("resource", ResourceTypes.KeyOf("timelines", "tl_screenshot"), false);
                for (var i = 0; i < 6; i++)
                {
                    await Task.Delay(150);
                    Dispatcher.UIThread.RunJobs();
                }

                Shoot(window, Path.Combine(folder, "02-timelines.png"));
                workspace.Data.Timelines.Remove(shotTimeline);

                // A code entry with GML in it, and the Settings page.
                var codeName = workspace.Data.Code.Where(c => c.Name.Content is { } n && n.Contains("Step_0", StringComparison.Ordinal)).OrderBy(c => c.Name.Content!.Length).First().Name.Content!;
                window.Docking.OpenTab("code", ResourceTypes.KeyOf("code", codeName), false);
                for (var i = 0; i < 40; i++)
                {
                    await Task.Delay(500);
                    Dispatcher.UIThread.RunJobs();
                }

                Shoot(window, Path.Combine(folder, "02-code.png"));
                window.Registry.Execute("settings.open");
                for (var i = 0; i < 4; i++)
                {
                    await Task.Delay(100);
                    Dispatcher.UIThread.RunJobs();
                }

                Shoot(window, Path.Combine(folder, "02-settings.png"));
                Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(window).OfType<Button>().First(b => b.Name == "SettingsClose").RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                window.Explorer.Select(ResourceTypes.KeyOf("objects", workspace.Data.GameObjects.First(o => o.Sprite is not null).Name.Content!));
                window.Registry.Execute("edit.findUsages");
                for (var i = 0; i < 20; i++)
                {
                    await Task.Delay(300);
                    Dispatcher.UIThread.RunJobs();
                }

                foreach (var panel in (string[])["git", "problems", "output", "usages"])
                {
                    window.Registry.Execute("view.panel." + panel);
                    for (var i = 0; i < 4; i++)
                    {
                        await Task.Delay(150);
                        Dispatcher.UIThread.RunJobs();
                    }

                    Shoot(window, Path.Combine(folder, $"04-panel-{panel}.png"));
                }

                // The tool windows, shown on their own so each picture is only that window.
                var host = new G3MStudio.Core.Plugins.PluginHost(new UserData(dir.File("plugin-user")), new G3MStudio.Core.Commands.CommandRegistry(), () => null, (_, _) => { });
                host.CreateTemplate("Sample Plugin");
                host.Load();
                var room = workspace.Data.Rooms.OrderByDescending(r => RoomInstanceCommand.Lists(r).Sum(l => l.Count)).First();
                Window[] tools = [new StringsWindow(workspace), new PluginsWindow(host, (_, _) => Task.FromResult(true)), new CreationOrderWindow(room, workspace)];
                foreach (var tool in tools)
                {
                    tool.Show();
                    for (var i = 0; i < 4; i++)
                    {
                        await Task.Delay(100);
                        Dispatcher.UIThread.RunJobs();
                    }

                    ShootWindow(tool, Path.Combine(folder, $"03-{tool.GetType().Name}.png"));
                    tool.Close();
                }
            }
            finally { window.Close(); }
        });
    }

    private static void Shoot(MainWindow window, string path)
    {
        window.UpdateLayout();
        var size = new PixelSize((int)window.Bounds.Width, (int)window.Bounds.Height);
        using var target = new RenderTargetBitmap(size);
        target.Render(window);
        target.Save(path, new PngBitmapEncoderOptions());
    }

    private static void ShootWindow(Window window, string path)
    {
        window.UpdateLayout();
        var size = new PixelSize(Math.Max(1, (int)window.Bounds.Width), Math.Max(1, (int)window.Bounds.Height));
        using var target = new RenderTargetBitmap(size);
        target.Render(window);
        target.Save(path, new PngBitmapEncoderOptions());
    }
}
