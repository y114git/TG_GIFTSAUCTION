namespace G3MStudio.Editor;

/// <summary>The Reduce motion setting: while it is on, previews show a still picture instead of moving.</summary>
public static class UiMotion
{
    public static bool Reduced { get; private set; }

    public static event Action? Changed;

    public static void Set(bool reduced)
    {
        if (reduced == Reduced) return;
        Reduced = reduced;
        Changed?.Invoke();
    }
}
