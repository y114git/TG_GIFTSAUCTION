using G3MStudio.Core.Settings;

namespace G3MStudio.Editor;

/// <summary>The room editor options of the Settings page (UTMT-SET-13): one grid size for every room instead of the grid each room stores. Every open room follows a change at once.</summary>
public static class RoomOptions
{
    public static bool GridOverride { get; private set; }

    public static int GridWidth { get; private set; } = 16;

    public static int GridHeight { get; private set; } = 16;

    public static AutoTileOptions AutoTile { get; private set; } = new();

    public static event Action? Changed;

    public static void SetAutoTile(AutoTileOptions options) => AutoTile = options;

    public static void Set(bool gridOverride, int width, int height)
    {
        if ((gridOverride, width, height) == (GridOverride, GridWidth, GridHeight)) return;
        (GridOverride, GridWidth, GridHeight) = (gridOverride, width, height);
        Changed?.Invoke();
    }

    /// <summary>The grid a room shows: the override when it is on, else the room's own.</summary>
    public static (double Width, double Height) GridFor(double roomWidth, double roomHeight) => GridOverride ? (GridWidth, GridHeight) : (roomWidth, roomHeight);
}
