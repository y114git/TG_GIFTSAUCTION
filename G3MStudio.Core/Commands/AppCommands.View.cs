namespace G3MStudio.Core.Commands;

public static partial class AppCommands
{
    private static void AddViewAndTools(CommandRegistry registry, IShell shell)
    {
        void Add(string id, string? shortcut, Action run, string? when = null) => registry.Add(new Command(id, shortcut, run, when));

        Add("view.commandPalette", "Ctrl+Shift+P", shell.ShowPalette);
        Add("view.fullscreen", "F11", shell.ToggleFullscreen);
        Add("view.zoomIn", "Ctrl+OemPlus", () => shell.Zoom(1));
        Add("view.zoomOut", "Ctrl+OemMinus", () => shell.Zoom(-1));
        Add("view.zoomReset", "Ctrl+D0", () => shell.Zoom(0));
        Add("tabs.close", "Ctrl+W", shell.CloseTab, WorkspaceOpen);
        Add("tabs.closeOthers", null, shell.CloseOtherTabs, WorkspaceOpen);
        Add("tabs.closeAll", "Ctrl+Shift+W", shell.CloseAllTabs, WorkspaceOpen);
        Add("tabs.reopen", "Ctrl+Shift+T", shell.ReopenTab, WorkspaceOpen);
        Add("tabs.back", "Alt+Left", shell.TabBack, WorkspaceOpen);
        Add("tabs.forward", "Alt+Right", shell.TabForward, WorkspaceOpen);
        Add("tabs.pin", null, shell.PinTab, WorkspaceOpen);
        Add("tabs.keepOpen", null, shell.KeepTabOpen, WorkspaceOpen);
        Add("tabs.splitRight", "Ctrl+OemPipe", shell.SplitRight, WorkspaceOpen);
        Add("go.quickOpen", "Ctrl+P", shell.QuickOpen, WorkspaceOpen);
        Add("go.goTo", "Ctrl+T", shell.GoTo, WorkspaceOpen);
        Add("settings.open", "Ctrl+OemComma", shell.ShowSettings);
        Add("tools.plugins", null, shell.ShowPlugins);
        Add("tools.strings", null, shell.ShowStrings, WorkspaceOpen);
        Add("tools.roomOrder", null, shell.ShowRoomOrder, WorkspaceOpen);
        Add("tools.textureGroups", null, shell.ShowTextureGroups, WorkspaceOpen);
        Add("tools.audioGroups", null, shell.ShowAudioGroups, WorkspaceOpen);
        Add("tools.hexView", null, shell.ShowHexView, WorkspaceOpen);
        Add("tools.offsetMap", null, shell.WriteOffsetMap, WorkspaceOpen);
        Add("tools.writeDebugData", null, shell.WriteDebugData, WorkspaceOpen);
        Add("tools.disassemble", null, shell.ShowDisassembly, WorkspaceOpen);
        Add("help.gettingStarted", null, shell.ShowOnboarding);
        Add("help.tour", null, shell.ShowTour);
        Add("window.layout.studio", null, () => shell.UseLayoutPreset("studio"), WorkspaceOpen);
        Add("window.layout.code", null, () => shell.UseLayoutPreset("code"), WorkspaceOpen);
        Add("window.layout.canvas", null, () => shell.UseLayoutPreset("canvas"), WorkspaceOpen);
        Add("window.layout.wide", null, () => shell.UseLayoutPreset("wide"), WorkspaceOpen);
        Add("window.layout.minimal", null, () => shell.UseLayoutPreset("minimal"), WorkspaceOpen);
        Add("window.layout.save", null, shell.SaveLayout, WorkspaceOpen);
        Add("window.layout.reset", null, shell.ResetLayout, WorkspaceOpen);
        Add("dock.toggleAll", "Ctrl+Alt+H", shell.ToggleDocks, WorkspaceOpen);
        Add("dock.toggleLeft", null, () => shell.ToggleDock("left"), WorkspaceOpen);
        Add("dock.toggleRight", null, () => shell.ToggleDock("right"), WorkspaceOpen);
        Add("dock.toggleBottom", null, () => shell.ToggleDock("bottom"), WorkspaceOpen);
        Add("window.showAll", null, shell.ShowAllWindows, WorkspaceOpen);
        Add("window.closeAll", null, shell.CloseAllWindows, WorkspaceOpen);
        Add("window.layout.saved", null, shell.ShowLayouts, WorkspaceOpen);
        Add("tools.runScript", null, shell.RunScript, WorkspaceOpen);
        Add("tools.runCommand", null, shell.RunCommand, WorkspaceOpen);
        Add("help.about", null, shell.ShowAbout);
        Add("help.openDataFolder", null, shell.OpenDataFolder);
        Add("help.documentation", "F1", shell.OpenDocumentation);
    }
}
