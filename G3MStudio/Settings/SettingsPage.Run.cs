using Avalonia.Controls;
using Avalonia.Media;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

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

        var profiles = new ComboBox { Name = "RunProfiles", MinWidth = Wide(12.5), ItemsSource = s.RunProfiles.Select(p => p.Name).ToList(), SelectedItem = s.RunProfiles.Any(p => p.Name == s.ActiveRunProfile) ? s.ActiveRunProfile : null };
        profiles.SelectionChanged += (_, _) =>
        {
            if (_building || profiles.SelectedItem is not string pick || _store.Current.RunProfiles.FirstOrDefault(p => p.Name == pick) is not { } profile) return;
            _store.Update(x => x with { ActiveRunProfile = pick, RunRunner = profile.Runner, RunArguments = profile.Arguments, RunDebugOutput = profile.DebugOutput, RunWorkingDir = profile.WorkingDir, RunEnvironment = profile.Environment });
            workDir.Text = profile.WorkingDir;
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
            var saved = new RunProfile(name, now.RunRunner, now.RunArguments, now.RunDebugOutput, now.RunWorkingDir, now.RunEnvironment);
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
            Row("live.environment", environment),
            Row("live.stopSeconds", Number("RunStopSeconds", 1, 60, 1, s.RunStopSeconds, (x, n) => x with { RunStopSeconds = n })),
            Row("settings.runtimeFolders", runtimeFolders),
            debugOutput,
            inPlace,
            autoRestart,
            warnTemp,
            Row("settings.profiles", profiles, deleteProfile),
            new TextBlock { Text = Loc.Current["settings.profilesNote"], Classes = { "muted" }, TextWrapping = TextWrapping.Wrap },
            Row("settings.profileName", profileName, saveProfile),
            .. RunProjectControls(s),
        ];
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
                var written = RunPlan.SaveProject(now, _store.UserData, file, new RunProfile("", now.RunRunner, now.RunArguments, now.RunDebugOutput, now.RunWorkingDir, now.RunEnvironment));
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
        return [Row("live.optionsLocation", location), Row("live.project", saveProject), projectNote];
    }
}
