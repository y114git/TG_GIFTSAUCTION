using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using G3MStudio.Commands;
using G3MStudio.Core.Commands;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>Collects what goes wrong while commands run: a throw, a slow call, an exception nobody observed.</summary>
internal sealed class SweepProbe : IDisposable
{
    private readonly EventHandler<UnobservedTaskExceptionEventArgs> _task;
    private readonly DispatcherUnhandledExceptionEventHandler _ui;

    public SweepProbe()
    {
        _task = (_, e) => Errors.Add($"{Where}: unobserved task exception: {e.Exception.GetBaseException()}");
        _ui = (_, e) =>
        {
            Errors.Add($"{Where}: unhandled UI exception: {e.Exception}");
            e.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += _task;
        Dispatcher.UIThread.UnhandledException += _ui;
    }

    public List<string> Errors { get; } = [];

    public string Where { get; set; } = "";

    /// <summary>Commands that opened a window, by the state they ran in.</summary>
    public SortedSet<string> Dialogs { get; } = [];

    /// <summary>Commands whose second call opened one more window of a kind that was already open.</summary>
    public SortedSet<string> Stacked { get; } = [];

    public List<(double Seconds, string Where)> Timings { get; } = [];

    public void Dispose()
    {
        TaskScheduler.UnobservedTaskException -= _task;
        Dispatcher.UIThread.UnhandledException -= _ui;
    }

    /// <summary>Faults of fire-and-forget tasks are reported when the task is collected.</summary>
    public static void Flush()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
    }
}

/// <summary>Runs every command of a window's registry through the path the menu uses, in the state the window is in.</summary>
internal static class CommandSweep
{
    // A shared runner is several times slower than a developer machine (the Intel macOS runner took 8.1 s to show the advanced settings page), so the limit stretches by the measured slowdown like the time budgets.
    private static readonly Lazy<TimeSpan> SlowLimit = new(() => TimeSpan.FromSeconds(8 * Budget.Slowdown));
    private static TimeSpan Slow => SlowLimit.Value;

    // Ends or replaces the state the sweep stands in, so each runs on its own.
    public static readonly string[] Special = ["file.close", "file.newData", "file.exit"];

    public static MainWindow NewWindow(TempDir dir, bool onboardingDone)
    {
        var window = TabsWorkspaceTests.NewWindow(dir);
        window.Workspaces.Settings.Update(x => x with { OnboardingDone = onboardingDone });
        Seams(window, false);
        return window;
    }

    /// <summary>Nothing blocks or reaches the machine: pickers cancel, questions get one fixed answer, the browser and the network are replaced.</summary>
    public static void Seams(MainWindow window, bool yes)
    {
        window.FilePicker = (_, _) => Task.FromResult<string?>(null);
        window.FilesPicker = _ => Task.FromResult<IReadOnlyList<string>>([]);
        window.FolderPicker = _ => Task.FromResult<string?>(null);
        window.Confirmer = (_, _) => Task.FromResult(yes);
        window.TransferChooser = () => Task.FromResult<string?>(null);
        window.CopyTargetChooser = _ => Task.FromResult<Workspace?>(null);
        window.DiffRightChooser = _ => Task.FromResult<Workspace?>(null);
        window.PackInfoAsker = (_, _) => Task.FromResult<G3MStudio.Core.ModPack.PackInfo?>(null);
        window.MomentCopyAsker = () => Task.FromResult<(bool, bool)?>(null);
        window.SaveAsker = _ => Task.FromResult<(bool? Sync, bool Remember)?>((null, false));
        window.AskClose = _ => Task.FromResult(yes ? CloseChoice.DoItLater : CloseChoice.Cancel);
        window.UpdateCheck = _ => throw new HttpRequestException("no network");
    }

    /// <summary>Lets posted jobs and the continuations of started tasks run; the timer wait is the cost, so it happens once.</summary>
    public static async Task Pump(int rounds = 1)
    {
        for (var i = 0; i < rounds; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Yield();
        }

        await Task.Delay(1);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>Pumps until the condition holds, at most 30 seconds; the caller asserts the state afterwards.</summary>
    public static async Task Until(Func<bool> condition)
    {
        var limit = Stopwatch.StartNew();
        while (!condition() && limit.Elapsed < TimeSpan.FromSeconds(30)) await Pump();
    }

    /// <summary>An edit made while a save or export of an earlier command is still running is queued, not applied; this waits for that to end so the edit lands at once.</summary>
    public static async Task EditWhenFree(Workspace workspace, EditCommand command)
    {
        await Until(() => !workspace.History.Frozen);
        workspace.Edit(command);
    }

    public static List<Window> Dialogs(Window owner)
    {
        var all = new List<Window>();
        foreach (var child in owner.OwnedWindows.Where(w => w.IsVisible))
        {
            all.Add(child);
            all.AddRange(Dialogs(child));
        }

        return all;
    }

    public static async Task CloseDialogs(MainWindow window, SweepProbe probe)
    {
        for (var round = 0; round < 5 && Dialogs(window) is { Count: > 0 } open; round++)
        {
            foreach (var dialog in open.AsEnumerable().Reverse()) Step(probe, "close " + dialog.GetType().Name, dialog.Close);
            await Pump();
        }
    }

    public static void Step(SweepProbe probe, string what, Action action)
    {
        var watch = Stopwatch.StartNew();
        try
        {
            action();
            Dispatcher.UIThread.RunJobs();
        }
        catch (Exception e)
        {
            probe.Errors.Add($"{probe.Where} [{what}]: {e}");
        }

        if (watch.Elapsed > Slow) probe.Errors.Add($"{probe.Where} [{what}]: blocked the UI thread for {watch.Elapsed.TotalSeconds:0.0} s");
    }

    private static void Invoke(MainWindow window, SweepProbe probe, Command command, string what) =>
        Step(probe, what, () => new RegistryCommand(window.Registry, command).Execute(null));

    private static List<Button> Buttons(Window dialog)
    {
        dialog.UpdateLayout();
        return [.. dialog.GetVisualDescendants().OfType<Button>().Where(b => b.IsVisible && b.IsEffectivelyEnabled)];
    }

    /// <summary>Runs the commands (default: all but <see cref="Special"/>) twice in a row, then once more per button of each dialog they open.</summary>
    public static async Task Run(MainWindow window, SweepProbe probe, string state, bool buttons, Func<Command, bool>? filter = null, Action<MainWindow>? before = null, CancellationToken cancel = default)
    {
        foreach (var command in window.Registry.All.OrderBy(c => c.Id, StringComparer.Ordinal).Where(c => !Special.Contains(c.Id) && (filter is null || filter(c))).ToList())
        {
            cancel.ThrowIfCancellationRequested();
            probe.Where = $"{state}: {command.Id}";
            if (!window.Registry.IsEnabled(command))
            {
                Step(probe, "forced while disabled", () =>
                {
                    if (window.Registry.Execute(command.Id)) probe.Errors.Add($"{probe.Where}: ran although its context is off");
                });
                continue;
            }

            var watch = Stopwatch.StartNew();
            before?.Invoke(window);
            var known = Dialogs(window);
            // Twice with the first run's windows still open: the second call must not stack, throw or block.
            Invoke(window, probe, command, "first");
            Dispatcher.UIThread.RunJobs();
            var opened = Dialogs(window).Except(known).ToList();
            Invoke(window, probe, command, "second, dialog open");
            await Pump();
            if (opened.Count > 0) probe.Dialogs.Add(command.Id);
            if (Dialogs(window).Except(known).Count() > opened.Count) probe.Stacked.Add(command.Id);
            await CloseDialogs(window, probe);
            if (buttons && opened.Count > 0) await EveryButton(window, probe, command, [.. opened.Select(d => d.GetType())], before);
            probe.Timings.Add((watch.Elapsed.TotalSeconds, probe.Where));
            if (Dialogs(window).Count > 0) probe.Errors.Add($"{probe.Where}: a window stayed open after it was closed");
        }

        probe.Where = state + ": end";
        SweepProbe.Flush();
    }

    private static async Task EveryButton(MainWindow window, SweepProbe probe, Command command, List<Type> types, Action<MainWindow>? before)
    {
        for (var at = 0; at < types.Count; at++)
        {
            var count = 0;
            // The button count is read from a fresh window each time; the loop stops when the index runs past the last button.
            for (var k = 0; ; k++)
            {
                before?.Invoke(window);
                var known = Dialogs(window);
                Invoke(window, probe, command, $"for button {k}");
                await Pump();
                var dialogs = Dialogs(window).Except(known).ToList();
                if (at >= dialogs.Count || dialogs[at].GetType() != types[at]) break;
                var found = Buttons(dialogs[at]);
                count = found.Count;
                if (k >= count)
                {
                    await CloseDialogs(window, probe);
                    break;
                }

                var button = found[k];
                probe.Where = $"{probe.Where.Split(" [")[0]} -> {types[at].Name}.{button.Name ?? (button.Content as string) ?? k.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
                Step(probe, "click", () => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)));
                await Pump();
                await CloseDialogs(window, probe);
                probe.Where = probe.Where.Split(" -> ")[0];
            }
        }
    }

    public static async Task Open(MainWindow window, string path) => await window.OpenAsync(path);
}
