using G3MStudio.Core.Workspace;

namespace G3MStudio.Docking;

/// <summary>Previous Tab and Next Tab: back and forward through the tabs in the order they were in front.</summary>
public sealed partial class StudioDock
{
    private readonly ActivationHistory _history = new();
    private bool _stepping;

    private bool Open(string id) => DocumentOf(id) is not null;

    /// <summary>True when <see cref="StepHistory"/> in the direction (-1 back, 1 forward) would reach a tab that is still open.</summary>
    public bool CanStepHistory(int direction) => _history.Can(direction, Open);

    /// <summary>Brings the tab used before (-1) or after (1) the one in front to the front, skipping closed tabs. The move itself is not recorded.</summary>
    public bool StepHistory(int direction)
    {
        if (_history.Step(direction, Open) is not { } id || DocumentOf(id) is not { } doc) return false;
        _stepping = true;
        try { Activate(doc); }
        finally { _stepping = false; }
        Changed?.Invoke();
        return true;
    }
}
