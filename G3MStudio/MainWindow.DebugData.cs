using G3MLib.DataFile;
using G3MLib.DataFile.ModelsDebug;
using G3MStudio.Core.Notifications;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>Writing the data of the GameMaker 1.4 debugger.</summary>
public sealed partial class MainWindow
{
    public void WriteDebugData() => _ = WriteDebugDataAsync();

    /// <summary>Asks what the source text holds and writes <c>FILE.yydebug</c> next to the data file, from the model as it is now (UTMT-RUN-05, UTMT-FMT-09).</summary>
    public async Task WriteDebugDataAsync()
    {
        if (Workspaces.Current is not { } workspace || !ApplyCodeEditors()) return;
        DebugDataMode[] modes = [DebugDataMode.Decompiled, DebugDataMode.Assembler, DebugDataMode.FullAssembler];
        var labels = new[] { Loc.Current["debug.decompiled"], Loc.Current["debug.assembler"], Loc.Current["debug.fullAssembler"] };
        if (await ProjectDialogs.AskChoiceAsync(this, Loc.Current["cmd.tools.writeDebugData"].TrimEnd('.'), Loc.Current["debug.pick"], labels) is not { } at) return;
        var target = Path.ChangeExtension(Path.GetFullPath(workspace.Path), ".yydebug");
        Notify(NotificationLevel.Info, Loc.Current["debug.working"]);
        try
        {
            await Task.Run(() =>
            {
                GameMakerDebugData debug;
                using (workspace.Lock.Read()) debug = DebugDataGenerator.Generate(workspace.Data, modes[at]);
                using var stream = File.Create(target);
                GameMakerDebugIO.Write(stream, debug);
            });
            Notify(NotificationLevel.Info, Loc.Current.Format("debug.done", ("file", Path.GetFileName(target))));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or GameMakerSerializationException)
        {
            Notify(NotificationLevel.Error, Loc.Current.Format("debug.failed", ("reason", e.Message)));
        }
    }
}
