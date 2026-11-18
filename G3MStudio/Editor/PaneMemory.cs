namespace G3MStudio.Editor;

/// <summary>Where an editor keeps a size the user dragged (the height of the properties under a sound, a sprite, an object...), by key; the main window keeps them in the settings.</summary>
public sealed record PaneMemory(Func<string, double?> Get, Action<string, double> Set);
