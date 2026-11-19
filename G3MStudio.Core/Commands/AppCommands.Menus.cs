namespace G3MStudio.Core.Commands;

public static partial class AppCommands
{
    private static void AddOrganize(CommandRegistry registry, IShell shell)
    {
        void Add(string id, Action run) => registry.Add(new Command(id, null, run, WorkspaceOpen));

        Add("organize.newFolder", shell.NewFolder);
        Add("organize.moveToFolder", shell.MoveToFolder);
        Add("organize.copyToFolder", shell.CopyToFolder);
        Add("organize.cutToFolder", shell.CutToFolder);
        Add("organize.pasteIntoFolder", shell.PasteIntoFolder);
        Add("organize.addToCollection", shell.AddToCollection);
        Add("organize.removeFromGroup", shell.RemoveFromGroup);
        Add("organize.colourLabel", shell.ColourLabel);
        Add("organize.bulkRename", shell.BulkRename);
        Add("organize.deadResources", shell.ShowDeadResources);
        Add("organize.toggleFolderView", shell.ToggleFolderView);
        Add("organize.selectByType", shell.SelectByType);
        Add("organize.selectContents", shell.SelectContents);
        Add("organize.moveToTextureGroup", shell.MoveToTextureGroup);
        Add("organize.moveToAudioGroup", shell.MoveToAudioGroup);
        Add("organize.openFolder", shell.OpenFolder);
        Add("organize.folderUp", shell.FolderUp);
        Add("organize.exportPackage", shell.ExportPackage);
    }

    /// <summary>The "New" submenu of the File menu: one command per type that can be created from nothing.</summary>
    public static readonly string[] ResourceNew = [.. Workspace.ResourceListCommand.Creatable.Select(t => "file.new." + t)];

    /// <summary>Menus that show only while their context is set (the tab in front or the Explorer selection is a room, an image, code, a sprite or a sound); the other menus always show.</summary>
    public static readonly IReadOnlyDictionary<string, string> MenuContexts = new Dictionary<string, string>
    {
        ["menu.sprite"] = SpriteTab,
        ["menu.sound"] = SoundTab,
        ["menu.font"] = FontTab,
        ["menu.tileSet"] = TileSetTab,
        ["menu.room"] = RoomTab,
        ["menu.image"] = ImageTab,
        ["menu.code"] = CodeTab,
    };

    /// <summary>The commands every resource menu shares (Explorer row, tab, code editor, room canvas), in this order; a menu leaves out the ones its resource type cannot take.</summary>
    /// <summary>The ways of moving resources that the single Transfer entry offers; they stay commands (palette, Explorer menu, key bindings) but have no menu entry of their own.</summary>
    public static readonly (string TitleKey, string[] Ids)[] TransferGroups =
    [
        ("transfer.groupIn", ["file.import", "file.importPackage", "file.importFiles", "file.importSheetNew", "file.importCodeFolder", "file.importCodeFiles"]),
        ("transfer.groupOut", ["edit.copyToOtherFile", "organize.exportPackage", "file.exportSelection", "file.exportAll"]),
        ("transfer.groupMods", ["mods.exportMod", "mods.exportPack"]),
    ];

    public static readonly string[] TransferModes = [.. TransferGroups.SelectMany(g => g.Ids)];

    public static readonly string[] ResourceMenu = ["edit.rename", "edit.editImage", "edit.duplicate", "edit.copyName", "edit.findUsages", "edit.copyToOtherFile", "edit.reveal", "edit.toggleFavourite", "edit.addTag"];

    /// <summary>The Sprite menu: every action of the sprite editor buttons (frames, resize, playback toggles), then the mask and GIF actions. The playback and view toggles stay here: the Explorer and tab menus of a sprite only add Resize Image/Canvas.</summary>
    public static readonly string[] SpriteMenu = ["edit.editImage", "edit.addFrame", "edit.duplicateFrame", "edit.removeFrame", "edit.replaceFrame", "edit.importSheet", "edit.resizeSprite", "-",
        "edit.spritePlay", "edit.spriteLoop", "edit.spriteOnion", "edit.spriteLockOrigin", "-", "edit.recalculateMask", "edit.importMask", "edit.exportMask", "edit.exportGif"];

    /// <summary>The menu of an editor tab: tab commands, then the resource commands.</summary>
    public static readonly string[] TabMenu = ["tabs.close", "tabs.closeOthers", "tabs.closeAll", "tabs.reopen", "-", "tabs.pin", "tabs.keepOpen", "tabs.splitRight"];

    /// <summary>Commands the menu bar leaves out because another place holds them (the tab menu, a button of a settings page). The palette and the shortcut editor still list them.</summary>
    public static readonly string[] OutsideMenuBar = [.. TabMenu.Where(id => id != "-"), "run.migrateOptions", "settings.resetPrompts"];

    /// <summary>The settings pages in the order of the Settings page, as language keys <c>settings.&lt;name&gt;</c>.</summary>
    public static readonly string[] SettingsPages = ["appearance", "colors", "editor", "image", "rooms", "decompiler", "files", "projects", "run", "advanced", "privacy", "prompts", "modBase", "shortcuts"];

    /// <summary>The panels that can be shown again from View > Panels after they were closed. The key is the dock panel id.</summary>
    public static readonly string[] Panels = ["Explorer", "Search", "Inspector", "Problems", "Output", "Usages", "Reference", "History", "Git"];

    /// <summary>The first command of each group of the Code menu: a separator comes before it.</summary>
    private static readonly HashSet<string> CodeGroupStarts = ["code.parameterHints", "code.toggleComment", "code.toggleBookmark", "code.outline", "code.goToLine", "code.deleteLine"];

    /// <summary>
    /// Menu bar: title key and the entries in order. "-" is a separator. An id with three parts (view.panel.explorer) goes into a submenu
    /// named by its first two parts (menu.sub.view.panel).
    /// </summary>
    public static readonly (string TitleKey, string[] Ids)[] Menus =
    [
        ("menu.file", ["file.newData", .. ResourceNew, "file.open", "file.transfer", "-", "file.save", "file.saveAs", "file.saveCopy", "file.revert", "-", "settings.open", "file.close", "file.exit"]),
        ("menu.edit", ["edit.undo", "edit.redo", "-", "edit.rename", "edit.duplicate", "edit.moveUp", "edit.moveDown", "edit.copyName", "edit.toggleFavourite", "edit.addTag", "edit.delete", "edit.selectAll", "-", "edit.find", "edit.findInCode", "edit.replaceInCode", "-", "edit.findUsages", "edit.findUses", "edit.findUnreferenced", "edit.findChildren"]),
        ("menu.organize", ["organize.newFolder", "organize.moveToFolder", "organize.copyToFolder", "organize.cutToFolder", "organize.pasteIntoFolder", "organize.addToCollection", "organize.removeFromGroup", "organize.colourLabel", "-", "organize.bulkRename", "organize.deadResources", "organize.moveToTextureGroup", "organize.moveToAudioGroup", "-", "organize.selectByType", "organize.selectContents", "-", "organize.toggleFolderView", "organize.openFolder", "organize.folderUp"]),
        ("menu.view", ["view.commandPalette", "go.quickOpen", "go.goTo", "edit.reveal", "-", .. Panels.Select(p => "view.panel." + p.ToLowerInvariant()), "-", "view.zoomIn", "view.zoomOut", "view.zoomReset", "view.fullscreen", "-", "view.nextRegion", "view.prevRegion"]),
        ("menu.project", ["project.open", "project.close", "-", "project.new", "project.enable", "-", "project.sync", "project.resolve", "-", "project.options", "project.moveDataFile", "project.disable"]),
        ("menu.sprite", SpriteMenu),
        ("menu.sound", ["edit.editAudio", "edit.replaceAudio", "edit.generateSound", "edit.recordSound", "edit.exportAudio", "edit.exportAllAudio", "-", "edit.playSound", "edit.stopSound", "edit.loopSound"]),
        ("menu.font", ["edit.editImage", "edit.replaceFont"]),
        ("menu.tileSet", ["edit.editImage"]),
        ("menu.room", ["room.play", "-", "room.creationCode", "room.creationOrder", "room.exportTiles", "room.importTiles", "room.imageToTiles", "room.savePng", "-", "room.autoTile", "room.retileSelection", "-", "room.addLayer", "room.duplicateLayer", "-", "room.toggleGrid", "room.toggleSnap", "room.zoomFit", "room.zoomActual"]),
        ("menu.image", [.. ImageCommands.Select(c => c.Id)]),
        ("menu.code", [.. CodeCommands.Select(c => c.Id).SelectMany(id => CodeGroupStarts.Contains(id) ? new[] { "-", id } : [id])]),
        ("menu.run", ["run.game", "run.restart", "run.withRuntime", "run.stop"]),
        ("menu.mods", ["mods.createPatch", "mods.applyPatch", "mods.applyMod", "mods.mergePatches", "mods.validatePatch", "-", "mods.compareDataFiles", "mods.applyPack", "mods.rememberBase", "mods.clearBase"]),
        ("menu.tools", ["tools.strings", "tools.roomOrder", "tools.textureGroups", "tools.audioGroups", "-", "tools.hexView", "tools.offsetMap", "tools.exportChunk", "tools.importChunk", "tools.writeDebugData", "tools.disassemble", "-", "tools.repackTextures", "tools.exportPage", "tools.replacePage", "tools.resizePage", "-", "tools.runScript", "tools.runCommand", "tools.plugins"]),
        ("menu.window", ["tabs.next", "tabs.previous", "tabs.historyBack", "tabs.historyForward", "tabs.back", "tabs.forward", "-", "window.showAll", "window.closeAll"]),
        ("menu.layout", ["window.layout.studio", "window.layout.wide", "window.layout.code", "window.layout.canvas", "window.layout.minimal", "-", "window.layout.save", "window.layout.saved", "window.layout.reset", "-", "dock.toggleAll", "dock.toggleLeft", "dock.toggleRight", "dock.toggleBottom"]),
        ("menu.help", ["help.gettingStarted", "help.tour", "help.documentation", "help.checkUpdates", "-", "help.sendFeedback", "help.reportProblem", "help.copyDiagnostics", "-", "help.openDataFolder", "help.openLogsFolder", "-", "help.about"]),
    ];

    /// <summary>The submenu a menu entry goes into, as the part after "menu.sub.", or null for a flat entry. The Layout menu lists its items flat.</summary>
    public static string? Submenu(string menuTitleKey, string id)
    {
        if (menuTitleKey == "menu.layout") return null;
        if (ImageGroups.TryGetValue(id, out var group)) return group;
        var parts = id.Split('.');
        return parts.Length < 3 ? null : string.Join('.', parts[..2]);
    }

    /// <summary>The language key of a command title: a panel command is named like the panel itself, every other one "cmd.&lt;id&gt;".</summary>
    public static string TitleKey(string id) => id.StartsWith("view.panel.", StringComparison.Ordinal) ? "panel." + id["view.panel.".Length..] : "cmd." + id;
}
