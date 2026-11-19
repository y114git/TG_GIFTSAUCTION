using System.Diagnostics;
using System.Globalization;
using System.Text;
using G3MStudio.Core.Workspace;

namespace G3MStudio;

/// <summary>
/// Developer switch for measuring memory of the real program: <c>G3MStudio --memory-probe=out.txt data.win</c> runs the open, tabs, close tabs, close file and
/// reopen cycle and writes one line of process and collector numbers per phase, then exits. Nothing else uses it.
/// </summary>
public sealed partial class MainWindow
{
    internal async Task RunMemoryProbeAsync(string output, string? path, Action exit)
    {
        var lines = new StringBuilder();
        async Task Measure(string phase, int settleSeconds = 3, Action? act = null)
        {
            await Task.Delay(TimeSpan.FromSeconds(settleSeconds));
            act?.Invoke();
            var process = Process.GetCurrentProcess();
            process.Refresh();
            var ws = process.WorkingSet64 >> 20;
            var priv = process.PrivateMemorySize64 >> 20;
            var natural = GC.GetTotalMemory(false) >> 20;
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            var info = GC.GetGCMemoryInfo(GCKind.Any);
            var gens = "";
            foreach (var g in info.GenerationInfo) gens += (g.SizeAfterBytes >> 20) + "/";
            process.Refresh();
            lines.Append(CultureInfo.InvariantCulture, $"{phase}\tws={ws}\tprivate={priv}\theapNatural={natural}\theapAfterGc={GC.GetTotalMemory(false) >> 20}\tgens(gen0/1/2/loh/poh)={gens}\twsAfterGc={process.WorkingSet64 >> 20}\tcommittedGc={info.TotalCommittedBytes >> 20}\n");
            await File.WriteAllTextAsync(output, lines.ToString());
            // For an outside heap tool: wait here with the numbers written, so it can look at the heap of this phase.
            if (Environment.GetEnvironmentVariable("G3MSTUDIO_PROBE_HOLD") == phase) await Task.Delay(TimeSpan.FromSeconds(120));
        }

        async Task OpenTabs()
        {
            var data = Workspaces.Current!.Data;
            var wanted = new (string Type, int Count)[] { ("code", 5), ("sprites", 3), ("rooms", 3), ("embeddedTextures", 3), ("objects", 3), ("sounds", 2), ("fonts", 2), ("shaders", 2) };
            foreach (var (type, count) in wanted.Where(w => Environment.GetEnvironmentVariable("G3MSTUDIO_PROBE_KINDS") is not { } only || only.Split(',').Contains(w.Type)))
            {
                var items = ResourceTypes.All.First(t => t.Key == type).Items(data)?.Take(count).ToList() ?? [];
                foreach (var item in items)
                {
                    Docking.OpenTab(type == "code" ? "code" : "resource", ResourceTypes.KeyOf(type, ResourceTypes.NameOf(item)), false);
                    await Task.Delay(500);
                }
            }
        }

        try
        {
            await Measure("a-start", 5);
            if (path is null) return;
            var load = Stopwatch.StartNew();
            await OpenAsync(path);
            lines.Append(CultureInfo.InvariantCulture, $"open-ms\t{load.ElapsedMilliseconds}\tpeakWs={Process.GetCurrentProcess().PeakWorkingSet64 >> 20}\tpeakVirtual={Process.GetCurrentProcess().PeakVirtualMemorySize64 >> 20}\n");
            await Measure("b-open", 6);
            await OpenTabs();
            await Measure("c-tabs", 6);
            foreach (var tab in Docking.Tabs.Tabs.ToList()) Docking.CloseTab(tab.Id);
            await Measure("d-tabs-closed", 7);
            Workspaces.Close();
            await Measure("e-file-closed", 7);
            for (var i = 1; i <= (int.TryParse(Environment.GetEnvironmentVariable("G3MSTUDIO_PROBE_REOPENS"), out var reopens) ? reopens : 10); i++)
            {
                await OpenAsync(path);
                await OpenTabs();
                foreach (var tab in Docking.Tabs.Tabs.ToList()) Docking.CloseTab(tab.Id);
                Workspaces.Close();
                await Measure("f-reopen-" + i, 6);
            }
        }
        catch (Exception e)
        {
            lines.Append("error\t" + e + "\n");
            await File.WriteAllTextAsync(output, lines.ToString());
        }
        finally
        {
            exit();
        }
    }
}
