using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.Media;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using G3MStudio.Theme;

namespace G3MStudio.Settings;

public sealed partial class SettingsPage
{
    private Control[] RunSection(AppSettings s)
    {
        var runner = new TextBox { Name = "RunRunner", Text = s.RunRunner, MinWidth = Wide(22.5) };
        runner.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunRunner = (runner.Text ?? "").Trim() }); };
        var runtimeFolders = new TextBox { Name = "RuntimeFolders", Text = s.RuntimeFolders, MinWidth = Wide(22.5) };
        runtimeFolders.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RuntimeFolders = runtimeFolders.Text ?? "" }); };
        var runArguments = new TextBox { Name = "RunArguments", Text = s.RunArguments, MinWidth = Wide(22.5) };
        runArguments.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunArguments = runArguments.Text ?? "" }); };
        var workDir = new TextBox { Name = "RunWorkingDir", Text = s.RunWorkingDir, MinWidth = Wide(22.5) };
        workDir.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunWorkingDir = (workDir.Text ?? "").Trim() }); };
        var links = new TextBox { Name = "RunLinks", [ToolTip.TipProperty] = Loc.Current["settings.runLinksHint"], Text = s.RunLinks, AcceptsReturn = true, MinHeight = 60, MinWidth = Wide(22.5), TextWrapping = Avalonia.Media.TextWrapping.NoWrap };
        links.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunLinks = links.Text ?? "" }); };
        var environment = new TextBox { Name = "RunEnvironment", Text = s.RunEnvironment, MinWidth = Wide(22.5) };
        environment.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunEnvironment = environment.Text ?? "" }); };
        var inPlace = new CheckBox { Name = "RunSaveInPlace", Tag = Loc.Current["live.saveInPlace"], Content = Loc.Current["live.saveInPlace"], IsChecked = s.RunSaveInPlace };
        inPlace.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunSaveInPlace = inPlace.IsChecked == true }); };
        var autoRestart = new CheckBox { Name = "RunAutoRestart", Tag = Loc.Current["live.autoRestart"], Content = Loc.Current["live.autoRestart"], IsChecked = s.RunAutoRestart };
        autoRestart.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunAutoRestart = autoRestart.IsChecked == true }); };
        var warnTemp = new CheckBox { Name = "WarnTempRun", Tag = Loc.Current["settings.warnTempRun"], Content = Loc.Current["settings.warnTempRun"], IsChecked = s.WarnTempRun };
        warnTemp.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { WarnTempRun = warnTemp.IsChecked == true }); };
        var debugOutput = new CheckBox { Name = "RunDebugOutput", Tag = Loc.Current["settings.runDebugOutput"], Content = Loc.Current["settings.runDebugOutput"], IsChecked = s.RunDebugOutput };
        debugOutput.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunDebugOutput = debugOutput.IsChecked == true }); };

        var compat = CompatControls(s);
        var profiles = new ComboBox { Name = "RunProfiles", MinWidth = Wide(12.5), PlaceholderText = Loc.Current["settings.default"], ItemsSource = s.RunProfiles.Select(p => p.Name).ToList(), SelectedItem = s.RunProfiles.Any(p => p.Name == s.ActiveRunProfile) ? s.ActiveRunProfile : null };
        profiles.SelectionChanged += (_, _) =>
        {
            if (_building || profiles.SelectedItem is not string pick || _store.Current.RunProfiles.FirstOrDefault(p => p.Name == pick) is not { } profile) return;
            _store.Update(x => x.WithRunProfile(profile) with { ActiveRunProfile = pick });
            workDir.Text = profile.WorkingDir;
            compat.Show(profile);
            environment.Text = profile.Environment;
            runner.Text = profile.Runner;
            runArguments.Text = profile.Arguments;
            debugOutput.IsChecked = profile.DebugOutput;
        };
        var profileName = new TextBox { Name = "RunProfileName", MinWidth = 160, PlaceholderText = Loc.Current["settings.profileName"] };
        var saveProfile = new Button { Name = "SaveRunProfile", Content = Loc.Current["settings.profileSave"] };
        saveProfile.Click += (_, _) =>
        {
            if (profileName.Text?.Trim() is not { Length: > 0 } name) return;
            var now = _store.Current;
            var saved = now.ToRunProfile(name);
            _store.Update(x => x with { RunProfiles = [.. x.RunProfiles.Where(p => p.Name != name), saved], ActiveRunProfile = name });
            Content = Build();
        };
        var deleteProfile = new Button { Name = "DeleteRunProfile", Content = Loc.Current["settings.profileDelete"] };
        deleteProfile.Click += (_, _) =>
        {
            if (profiles.SelectedItem is not string pick) return;
            _store.Update(x => x with { RunProfiles = [.. x.RunProfiles.Where(p => p.Name != pick)], ActiveRunProfile = "" });
            Content = Build();
        };

        return
        [
            Heading("settings.run"),
            Row("settings.runRunner", runner),
            Row("settings.runArguments", runArguments),
            Row("live.workingDir", workDir),
            Row("settings.runLinks", links),
            Row("live.environment", environment),
            Row("live.stopSeconds", Number("RunStopSeconds", 1, 60, 1, s.RunStopSeconds, (x, n) => x with { RunStopSeconds = n })),
            Row("settings.runtimeFolders", runtimeFolders),
            .. compat.Controls,
            debugOutput,
            inPlace,
            autoRestart,
            warnTemp,
            Row("settings.profiles", profiles, deleteProfile),
            new TextBlock { Text = Loc.Current["settings.profilesNote"], Classes = { "muted" }, [HelpText.KindProperty] = HelpKind.Detail, TextWrapping = TextWrapping.Wrap },
            Row("settings.profileName", profileName, saveProfile),
            .. RunProjectControls(s),
        ];
    }

    /// <summary>The Compatibility group of the Run page: how Windows games start on Linux and macOS. <see cref="Show"/> puts a picked profile into the controls.</summary>
    private sealed record CompatGroup(Control[] Controls, Action<RunProfile> Show);

    private CompatGroup CompatControls(AppSettings s)
    {
        var modes = Enum.GetValues<CompatMode>();
        var mode = new ComboBox { Name = "RunCompat", ItemsSource = modes.Select(m => Loc.Current["compat.mode." + m]).ToList(), SelectedIndex = Array.IndexOf(modes, s.RunCompat), MinWidth = Wide(15) };
        var program = new TextBox { Name = "RunCompatProgram", Text = s.RunCompatProgram, MinWidth = Wide(22.5) };
        program.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunCompatProgram = (program.Text ?? "").Trim() }); };
        var browse = new Button { Name = "RunCompatBrowse", Content = Loc.Current["compat.browse"] };
        browse.Click += async (_, _) =>
        {
            if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage) return;
            var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions { AllowMultiple = false });
            if (picked.Count > 0 && picked[0].TryGetLocalPath() is { } path) program.Text = path;
        };
        var prefix = new TextBox { Name = "RunWinePrefix", Text = s.RunWinePrefix, MinWidth = Wide(22.5) };
        prefix.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunWinePrefix = (prefix.Text ?? "").Trim() }); };
        var command = new TextBox { Name = "RunCompatCommand", Text = s.RunCompatCommand, MinWidth = Wide(22.5) };
        command.TextChanged += (_, _) => { if (!_building) _store.Update(x => x with { RunCompatCommand = command.Text ?? "" }); };
        var commandRow = Row("compat.command", command);
        SetSelfHidden(commandRow, s.RunCompat != CompatMode.Custom);
        var detected = new TextBlock { Name = "RunCompatDetected", Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
        var detect = new Button { Name = "RunCompatDetect", Content = Loc.Current["compat.detect"] };
        void Detect() => detected.Text = DetectedText();
        detect.Click += (_, _) => Detect();
        Detect();
        mode.SelectionChanged += (_, _) =>
        {
            if (_building || mode.SelectedIndex < 0) return;
            _store.Update(x => x with { RunCompat = modes[mode.SelectedIndex] });
            SetSelfHidden(commandRow, modes[mode.SelectedIndex] != CompatMode.Custom);
        };
        var title = new TextBlock { Text = Loc.Current["compat.title"], FontWeight = FontWeight.SemiBold, Margin = new Thickness(0, 12, 0, 0), Tag = Loc.Current["compat.title"] };
        var detectedRow = Row("compat.detected", detected, detect);
        Control[] controls = [title, Row("compat.modeLabel", mode), Row("compat.program", program, browse), Row("compat.prefix", prefix), commandRow, detectedRow];
        return new CompatGroup(controls, p =>
        {
            mode.SelectedIndex = Array.IndexOf(modes, p.Compat);
            program.Text = p.CompatProgram;
            prefix.Text = p.WinePrefix;
            command.Text = p.CompatCommand;
            Detect();
        });
    }

    /// <summary>What Auto would use right now: the tool and its prefix, found again on every call.</summary>
    private string DetectedText()
    {
        if (OperatingSystem.IsWindows()) return Loc.Current["compat.notNeeded"];
        var now = _store.Current;
        var options = new CompatOptions(now.RunCompat, now.RunCompatProgram, now.RunWinePrefix, now.RunCompatCommand, now.RunEnvironment, now.RunWorkingDir);
        if (CompatRun.Detect(SystemProbe.Instance, options, DataFile()) is not { } found) return Loc.Current["compat.detectedNone"];
        var tool = found.Detail ?? Loc.Current["compat.mode." + found.Tool];
        return found.Prefix is { } p ? Loc.Current.Format("compat.detectedPrefix", ("tool", tool), ("prefix", p)) : Loc.Current.Format("compat.detectedTool", ("tool", tool));
    }

    private Control[] RunProjectControls(AppSettings s)
    {
        var projectNote = new TextBlock { Name = "RunProjectNote", Classes = { "muted" }, TextWrapping = TextWrapping.Wrap };
        var saveProject = new Button { Name = "SaveRunProject", Content = Loc.Current["live.saveProject"] };
        saveProject.Click += (_, _) =>
        {
            if (DataFile() is not { } file) { projectNote.Text = Loc.Current["live.noFile"]; return; }
            var now = _store.Current;
            try
            {
                var written = RunPlan.SaveProject(now, _store.UserData, file, now.ToRunProfile());
                // A file next to the data file counts only when approved; the owner wrote this one here. A file in Studio's folder needs no approval.
                if (now.RunOptionsLocation == RunOptionsLocation.BesideDataFile && RunPlan.ProjectHash(file) is { } hash) _store.Update(x => x with { TrustedRunFiles = [.. x.TrustedRunFiles, hash] });
                projectNote.Text = Loc.Current.Format("live.projectSaved", ("file", written));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                projectNote.Text = e.Message;
            }
        };
        var locations = Enum.GetValues<RunOptionsLocation>();
        var location = new ComboBox { Name = "RunOptionsLocation", ItemsSource = locations.Select(l => Loc.Current["live.location." + l]).ToList(), SelectedIndex = Array.IndexOf(locations, s.RunOptionsLocation), MinWidth = Wide(15) };
        location.SelectionChanged += (_, _) => { if (!_building && location.SelectedIndex >= 0) _store.Update(x => x with { RunOptionsLocation = locations[location.SelectedIndex] }); };
        // Moving the run file from beside the data file into Studio's folder is a setting of these options, so it sits with them (the command is also in the palette).
        var migrate = new Button { Name = "MigrateRunOptions", Content = Loc.Current["cmd.run.migrateOptions"] };
        migrate.Click += (_, _) => _commands.Execute("run.migrateOptions");
        return [Row("live.optionsLocation", location), Row("live.project", saveProject, migrate), projectNote];
    }
}
