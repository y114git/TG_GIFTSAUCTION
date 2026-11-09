namespace G3MStudio.Core.Workspace;

public enum RunState { Idle, Preparing, Running, Stopping }

/// <summary>How a run ended. A stop the user asked for is not a crash, whatever the exit code.</summary>
public sealed record RunExit(RunSpec Spec, int Code, bool Stopped, IReadOnlyList<string> Tail)
{
    public bool Crashed => Code != 0 && !Stopped;
}

/// <summary>One game process at a time: start, stop with a grace time, restart, and the output into <see cref="Log"/>. Call it from the UI thread; it reports back on that thread.</summary>
public sealed class LiveRun
{
    private GameRun? _game;
    private Task _watch = Task.CompletedTask;

    public RunLog Log { get; } = new();

    public RunState State { get; private set; }

    /// <summary>How long a stop waits for the game to close before it kills it.</summary>
    public TimeSpan Grace { get; set; } = TimeSpan.FromSeconds(5);

    public event Action? StateChanged;

    public event Action<RunExit>? Finished;

    /// <summary>Completes when no game runs.</summary>
    public Task WaitIdleAsync() => _watch;

    /// <summary>Gets the spec ready (the caller saves the working copy there) and starts it; false when nothing ran: one is running already, or prepare gave null.</summary>
    public async Task<bool> StartAsync(Func<Task<RunSpec?>> prepare)
    {
        if (State != RunState.Idle) return false;
        Set(RunState.Preparing);
        RunSpec? spec;
        try
        {
            spec = await prepare();
        }
        catch
        {
            Set(RunState.Idle);
            throw;
        }

        if (spec is null)
        {
            Set(RunState.Idle);
            return false;
        }

        try
        {
            _game = GameRun.Start(spec, (text, error) => Log.Add(text, error));
        }
        catch
        {
            Set(RunState.Idle);
            throw;
        }

        var game = _game;
        Set(RunState.Running);
        _watch = WatchAsync(game, spec);
        return true;
    }

    /// <summary>Stops the game it started (graceful, then kill) and starts again.</summary>
    public async Task<bool> RestartAsync(Func<Task<RunSpec?>> prepare)
    {
        await StopAsync();
        return await StartAsync(prepare);
    }

    public async Task StopAsync()
    {
        if (_game is not { } game || State != RunState.Running) return;
        Set(RunState.Stopping);
        await game.StopAsync(Grace);
        await _watch;
    }

    /// <summary>Kills the game at once, without waiting; for a window that closes.</summary>
    public void Kill() => _game?.Stop();

    private async Task WatchAsync(GameRun game, RunSpec spec)
    {
        int code;
        try
        {
            code = await game.Exited;
        }
        catch (Exception e) when (e is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            code = -1; // without this the state would stay Running for good
        }

        var exit = new RunExit(spec, code, game.StopRequested, game.Tail(10));
        game.Dispose();
        _game = null;
        Set(RunState.Idle);
        Finished?.Invoke(exit);
    }

    private void Set(RunState state)
    {
        State = state;
        StateChanged?.Invoke();
    }
}

