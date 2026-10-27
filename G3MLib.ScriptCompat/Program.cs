using System.Text;
using G3MLib.DataFile;
using G3MLib.DataFile.Scripting;
using Microsoft.CodeAnalysis.Scripting;

// Compiles every .csx script under a folder against the G3MLib script host and runs the ones that need no dialog on a copy of a data file.
// Usage: G3MLib.ScriptCompat <scripts folder> <data file> <work folder> <report.md>
if (args.Length != 4)
{
    Console.Error.WriteLine("Usage: G3MLib.ScriptCompat <scripts folder> <data file> <work folder> <report.md>");
    return 2;
}

string scriptsSource = Path.GetFullPath(args[0]);
string fixture = Path.GetFullPath(args[1]);
string work = Path.GetFullPath(args[2]);
string reportPath = Path.GetFullPath(args[3]);
TimeSpan timeout = TimeSpan.FromSeconds(60);

if (Directory.Exists(work)) Directory.Delete(work, recursive: true);
string scriptsCopy = Path.Combine(work, "Scripts");
CopyDirectory(scriptsSource, scriptsCopy);
Directory.SetCurrentDirectory(work);

List<Row> rows = [];
foreach (string script in Directory.EnumerateFiles(scriptsCopy, "*.csx", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
{
    string category = Path.GetFileName(Path.GetDirectoryName(script));
    string name = Path.GetFileName(script);
    Row row = new(name, category);
    rows.Add(row);

    try
    {
        ScriptRunner.Compile(script);
        row.Compiles = true;
    }
    catch (CompilationErrorException exception)
    {
        row.Reason = "compile: " + Shorten(string.Join("; ", exception.Diagnostics.Take(2).Select(d => d.Id + " " + d.GetMessage() + " (line " + (d.Location.GetLineSpan().StartLinePosition.Line + 1) + ")")));
        continue;
    }
    catch (Exception exception)
    {
        row.Reason = "compile: " + Shorten(exception.Message);
        continue;
    }

    string runDirectory = Path.Combine(work, "runs", rows.Count.ToString("000"));
    Directory.CreateDirectory(runDirectory);
    string dataPath = Path.Combine(runDirectory, Path.GetFileName(fixture));
    File.Copy(fixture, dataPath);
    using GameMakerData data = GameMakerIO.Read(File.OpenRead(dataPath));
    CompatGlobals globals = new(data, dataPath, script) { ExePath = work, ScriptRootPath = work, CanSave = true };
    Directory.SetCurrentDirectory(runDirectory);
    Task<ScriptRunResult> run = Task.Run(() => ScriptRunner.RunAsync(script, globals));
    if (!run.Wait(timeout))
    {
        row.Reason = "run: no result after " + timeout.TotalSeconds + " s";
        continue;
    }

    ScriptRunResult result = run.Result;
    if (globals.DialogCalled is not null)
    {
        row.Runs = "needs dialog";
        row.Reason = globals.DialogCalled;
    }
    else if (result.Success)
    {
        row.Runs = "yes";
    }
    else
    {
        row.Runs = "no";
        row.Reason = "run: " + Explain(result.Error);
    }
}

Directory.SetCurrentDirectory(work);
WriteReport(reportPath, rows);
Console.WriteLine($"scripts {rows.Count}, compile {rows.Count(r => r.Compiles)}, run {rows.Count(r => r.Runs == "yes")}, dialog {rows.Count(r => r.Runs == "needs dialog")}, run failed {rows.Count(r => r.Runs == "no")}");
Environment.Exit(0);
return 0;

static void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (string file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
    foreach (string directory in Directory.GetDirectories(source)) CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
}

string Explain(string error)
{
    const string trace = "The current stacktrace:";
    int split = error.IndexOf(trace, StringComparison.Ordinal);
    if (split < 0) return Shorten(error);
    string type = error["Script execution failed: ".Length..error.IndexOf(':', "Script execution failed: ".Length)];
    return Shorten(type + " at " + error[(split + trace.Length)..]);
}

string Shorten(string text)
{
    string line = text.Replace('\r', ' ').Replace('\n', ' ').Replace("|", "/").Trim();
    return line.Length <= 200 ? line : line[..197] + "...";
}

static void WriteReport(string path, List<Row> rows)
{
    StringBuilder report = new();
    report.AppendLine("| Script | Category | Compiles | Runs | Reason |");
    report.AppendLine("| --- | --- | --- | --- | --- |");
    foreach (Row row in rows)
        report.AppendLine($"| {row.Name} | {row.Category} | {(row.Compiles ? "yes" : "no")} | {row.Runs} | {row.Reason} |");
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, report.ToString());
}

sealed class Row(string name, string category)
{
    public string Name { get; } = name;
    public string Category { get; } = category;
    public bool Compiles { get; set; }
    public string Runs { get; set; } = "-";
    public string Reason { get; set; } = "";
}

sealed class CompatGlobals(GameMakerData data, string filePath, string scriptPath) : ScriptGlobals(data, filePath, scriptPath)
{
    public string DialogCalled { get; private set; }

    private Exception Dialog(string name)
    {
        DialogCalled ??= name;
        return new ScriptCancelledException("Dialog needed: " + name);
    }

    public override bool ScriptQuestion(string message) => throw Dialog(nameof(ScriptQuestion));
    public override string ScriptInputDialog(string title, string label, string defaultInput, string cancelText, string submitText, bool isMultiline, bool preventClose) => throw Dialog(nameof(ScriptInputDialog));
    public override string SimpleTextInput(string title, string label, string defaultValue, bool allowMultiline, bool showDialog = true) => throw Dialog(nameof(SimpleTextInput));
    public override string PromptChooseDirectory() => throw Dialog(nameof(PromptChooseDirectory));
    public override string PromptLoadFile(string defaultExt, string filter) => throw Dialog(nameof(PromptLoadFile));
    public override string PromptSaveFile(string defaultExt, string filter) => throw Dialog(nameof(PromptSaveFile));
    public override void ScriptOpenURL(string url) => throw Dialog(nameof(ScriptOpenURL));
}
