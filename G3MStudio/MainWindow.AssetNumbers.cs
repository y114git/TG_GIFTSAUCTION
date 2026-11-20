using G3MLib.Modding.Resources;
using G3MStudio.Core.Notifications;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Edit > Replace Numbers with Asset Names in All Code: one undoable edit over every code entry.</summary>
public sealed partial class MainWindow
{
    public void ReplaceAssetNumbers() => _ = ReplaceAssetNumbersAsync();

    private async Task ReplaceAssetNumbersAsync()
    {
        if (Workspaces.Current is not { } workspace) return;
        var settings = Workspaces.Settings.Current;
        var types = settings.CodeAssetNumberTypes;
        if (!await Confirm("assetNumbers", Loc.Current["cmd.edit.assetNumbers"].TrimEnd('.'), Loc.Current["code.assetNumbersAsk"])) return;
        AssetNumbersResult? result = null;
        try
        {
            await WithProgressAsync(workspace.Name, async (progress, token) =>
            {
                var report = new Progress<(int Done, int Total)>(p => progress.Report(new BatchProgress(p.Done, p.Total, "")));
                var parameters = await AssetNumbersApply.BuildParametersAsync(workspace, settings.CodeAssetNumbersGameFunctions, report, token);
                result = await AssetNumbersApply.RunAsync(workspace, parameters, types.Contains, report, token);
            });
        }
        catch (InvalidOperationException e)
        {
            Notify(NotificationLevel.Error, e.Message);
            return;
        }

        if (result is null) return;
        Notify(NotificationLevel.Info, result.Numbers == 0
            ? Loc.Current["code.assetNumbersNone"]
            : Loc.Current.Format("code.assetNumbersDone", ("numbers", result.Numbers), ("entries", result.Entries)));
        if (result.Failed.Count > 0) Notify(NotificationLevel.Warning, Loc.Current.Format("code.assetNumbersFailed", ("entries", string.Join(", ", result.Failed.Take(8)) + (result.Failed.Count > 8 ? ", ..." : ""))));
    }
}
