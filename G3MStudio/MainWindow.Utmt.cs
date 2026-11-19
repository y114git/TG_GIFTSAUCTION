using G3MLib.Modding.Resources;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Projects;
using G3MStudio.Core.Settings;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Project > Open Project (and a dropped folder): converts a UndertaleModTool project into a G3MStudio project and opens the result.</summary>
public sealed partial class MainWindow
{
    /// <summary>Replaces the dialog that asks for the data file and the new folder. Tests set it.</summary>
    public Func<UtmtProjectInfo, Task<UtmtImportRequest?>>? UtmtAsker { get; set; }

    /// <summary>Replaces the report dialog; true opens the new project. Tests set it.</summary>
    public Func<UtmtImportReport, Task<bool>>? UtmtReporter { get; set; }

    private static readonly Dictionary<string, string> StageKeys = new()
    {
        ["read"] = "project.utmt.stage.read",
        ["import"] = "project.utmt.stage.import",
        ["write"] = "project.utmt.stage.write",
        ["export"] = "project.utmt.stage.export",
        ["git"] = "project.utmt.stage.git",
    };

    /// <summary>Converts the UTMT project at <paramref name="path"/> (a folder or its project.json). Project > Open Project and a dropped folder reach it.</summary>
    public async Task OpenUtmtProjectAsync(string path)
    {
        // A dropped project runs without an awaiting caller, so every failure ends in a message; the expected import errors are not reported.
        try
        {
            await ConvertUtmtProjectAsync(path);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            if (e is not UtmtImportException) FailureReport.Report(e, "utmt.import");
            Notify(NotificationLevel.Error, Loc.Current.Format("project.utmt.failed", ("reason", e.Message)));
        }
    }

    private async Task ConvertUtmtProjectAsync(string path)
    {
        var info = UtmtProjectImporter.Inspect(path);
        var request = await (UtmtAsker?.Invoke(info) ?? ProjectDialogs.AskUtmtAsync(this, info, Workspaces.Projects.FreeFolder(info.Name), await GitService.IsInstalledAsync()));
        if (request is null) return;
        if (File.Exists(request.BaseDataFile) && await UtmtProjectImporter.CheckBaseAsync(info, request.BaseDataFile) == UtmtBaseCheck.Mismatch
            && !await Confirm(Loc.Current["project.utmt.mismatchTitle"], Loc.Current["project.utmt.mismatchAsk"])) return;

        (_exportName, _exportDone, _exportTotal) = (info.Name, 0, 0);
        using var source = _exporting = new CancellationTokenSource();
        ShowProgress();
        UtmtImportReport? report = null;
        try
        {
            var stage = new Progress<string>(s => { _exportName = $"{info.Name}: {Loc.Current[StageKeys[s]]}"; ShowProgress(); });
            report = await new UtmtProjectImporter(Workspaces.Projects).ImportAsync(request, stage, new Progress<BatchProgress>(p => { (_exportDone, _exportTotal) = (p.Done, p.Total); ShowProgress(); }), source.Token);
        }
        catch (OperationCanceledException)
        {
            Notify(NotificationLevel.Info, Loc.Current["project.cancelled"]);
        }
        finally
        {
            _exportName = null;
            _exporting = null;
            Progress.IsVisible = false;
        }

        if (report is null) return;
        if (await (UtmtReporter?.Invoke(report) ?? ProjectDialogs.ShowUtmtReportAsync(this, report))) await OpenAsync(report.DataFile);
    }
}
