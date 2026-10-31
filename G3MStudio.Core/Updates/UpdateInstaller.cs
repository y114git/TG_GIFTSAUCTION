using System.Diagnostics;
using System.IO.Compression;
using System.Text;

namespace G3MStudio.Core.Updates;

/// <summary>
/// Stages a verified zip next to the program files, then writes the native script (Q43) that waits for the app to exit,
/// moves each file in with a backup, rolls back on any failure, and starts the app again.
/// Layout under the program folder: .g3mstudio-update/new (staged files), old (backups), result.txt ("ok" or "failed").
/// shortcut: files that exist only in the old version stay in the program folder, fine for the single-file app, add a delete list if a release ever drops files.
/// </summary>
public static class UpdateInstaller
{
    public const string WorkFolder = ".g3mstudio-update";

    public static string ResultFile(string programDir) => Path.Combine(programDir, WorkFolder, "result.txt");

    /// <summary>Throws NotWritable naming the folder when a file cannot be created in it.</summary>
    public static void EnsureWritable(string programDir)
    {
        var probe = Path.Combine(programDir, $".write-test-{Guid.NewGuid():N}");
        try
        {
            File.WriteAllBytes(probe, []);
            File.Delete(probe);
        }
        catch (Exception e) when (e is UnauthorizedAccessException or IOException)
        {
            throw new UpdateException(UpdateError.NotWritable, $"Cannot write to {programDir}.", programDir);
        }
    }

    /// <summary>Extracts the whole zip into the staging folder (an earlier partial staging is removed first). Nothing in the program folder changes.</summary>
    public static string Stage(string zipPath, string programDir)
    {
        EnsureWritable(programDir);
        var staging = Path.Combine(programDir, WorkFolder, "new");
        Directory.CreateDirectory(Path.Combine(programDir, WorkFolder));
        if (Directory.Exists(staging)) Directory.Delete(staging, true);
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);
            zip.ExtractToDirectory(staging);
            // Keep the execute bit the zip carries; extraction alone may not set it on Unix.
            if (!OperatingSystem.IsWindows())
                foreach (var entry in zip.Entries.Where(e => e.Name.Length > 0 && ((e.ExternalAttributes >> 16) & 0x49) != 0))
                    File.SetUnixFileMode(Path.Combine(staging, entry.FullName), (UnixFileMode)((entry.ExternalAttributes >> 16) & 0x1FF));
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            throw new UpdateException(UpdateError.BadArchive, "The update file could not be unpacked.", zipPath);
        }
        return staging;
    }

    /// <summary>
    /// Writes the update script into scriptFolder and returns its path. relaunch is the path inside programDir that starts the new
    /// version (the exe, or the .app folder on macOS). pid is the process to wait for.
    /// </summary>
    public static string WriteScript(string programDir, string relaunch, int pid, string scriptFolder)
    {
        var staging = Path.Combine(programDir, WorkFolder, "new");
        var files = Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories).Select(f => Path.GetRelativePath(staging, f)).Order().ToList();
        if (files.Count == 0) throw new UpdateException(UpdateError.BadArchive, "The update file holds no files.", staging);
        Directory.CreateDirectory(scriptFolder);
        var path = Path.Combine(scriptFolder, $"g3mstudio-update-{pid}" + (OperatingSystem.IsWindows() ? ".cmd" : ".sh"));
        File.WriteAllText(path, OperatingSystem.IsWindows() ? WindowsScript(programDir, relaunch, pid, files) : UnixScript(programDir, relaunch, pid, files), new UTF8Encoding(false));
        return path;
    }

    /// <summary>Starts the script detached. The caller exits right after.</summary>
    public static void Launch(string script)
    {
        var start = OperatingSystem.IsWindows()
            ? new ProcessStartInfo("cmd", ["/d", "/c", script]) { CreateNoWindow = true }
            : new ProcessStartInfo("/bin/sh", ["-c", $"nohup sh {ShQuote(script)} >/dev/null 2>&1 &"]);
        start.UseShellExecute = false;
        Process.Start(start)?.Dispose();
    }

    private static List<string> Folders(List<string> files) =>
        [.. files.Select(f => Path.GetDirectoryName(f) ?? "").Where(d => d.Length > 0).Distinct().Order()];

    private static string CmdQuote(string path) => "\"" + path.Replace("%", "%%", StringComparison.Ordinal) + "\"";

    private static string ShQuote(string text) => "'" + text.Replace("'", "'\\''", StringComparison.Ordinal) + "'";

    private static string WindowsScript(string programDir, string relaunch, int pid, List<string> files)
    {
        var app = programDir.TrimEnd('\\');
        string App(string rel) => CmdQuote(Path.Combine(app, rel));
        string New(string rel) => CmdQuote(Path.Combine(app, WorkFolder, "new", rel));
        string Old(string rel) => CmdQuote(Path.Combine(app, WorkFolder, "old", rel));
        var result = CmdQuote(ResultFile(app));
        var folders = Folders(files);
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("chcp 65001 >nul");
        sb.AppendLine("set TRIES=0");
        sb.AppendLine(":wait");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"tasklist /FI \"PID eq {pid}\" /NH 2>nul | findstr /C:\" {pid} \" >nul");
        sb.AppendLine("if errorlevel 1 goto swap");
        sb.AppendLine("set /a TRIES+=1");
        sb.AppendLine("if %TRIES% GTR 120 goto timeout");
        sb.AppendLine("ping -n 2 127.0.0.1 >nul");
        sb.AppendLine("goto wait");
        sb.AppendLine(":swap");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"if not exist {CmdQuote(Path.Combine(app, WorkFolder, "old"))} mkdir {CmdQuote(Path.Combine(app, WorkFolder, "old"))}");
        foreach (var d in folders)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"if not exist {App(d)} mkdir {App(d)}");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"if not exist {Old(d)} mkdir {Old(d)}");
        }
        foreach (var f in files)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"if exist {App(f)} move /Y {App(f)} {Old(f)} >nul || goto rollback");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"move /Y {New(f)} {App(f)} >nul || goto rollback");
        }
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"rmdir /S /Q {CmdQuote(Path.Combine(app, WorkFolder, "old"))}");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"rmdir /S /Q {CmdQuote(Path.Combine(app, WorkFolder, "new"))}");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $">{result} echo ok");
        sb.AppendLine("goto start");
        sb.AppendLine(":rollback");
        foreach (var f in Enumerable.Reverse(files))
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"if exist {Old(f)} (move /Y {Old(f)} {App(f)} >nul) else if not exist {New(f)} del /F /Q {App(f)} 2>nul");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"rmdir /S /Q {CmdQuote(Path.Combine(app, WorkFolder, "new"))}");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"rmdir {CmdQuote(Path.Combine(app, WorkFolder, "old"))} 2>nul");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $">{result} echo failed");
        sb.AppendLine("goto start");
        sb.AppendLine(":timeout");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $">{result} echo failed: the app did not exit");
        sb.AppendLine("goto done");
        sb.AppendLine(":start");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"start \"\" /D {CmdQuote(app)} {App(relaunch)}");
        sb.AppendLine(":done");
        sb.AppendLine("(goto) 2>nul & del /F /Q \"%~f0\"");
        return sb.ToString();
    }

    public static string UnixScript(string programDir, string relaunch, int pid, List<string> files)
    {
        var app = programDir.TrimEnd('/');
        string App(string rel) => ShQuote(app + "/" + rel.Replace('\\', '/'));
        string New(string rel) => ShQuote(app + "/" + WorkFolder + "/new/" + rel.Replace('\\', '/'));
        string Old(string rel) => ShQuote(app + "/" + WorkFolder + "/old/" + rel.Replace('\\', '/'));
        var result = ShQuote(app + "/" + WorkFolder + "/result.txt");
        var folders = Folders(files);
        var target = app + "/" + relaunch.Replace('\\', '/');
        var sb = new StringBuilder();
        sb.AppendLine("#!/bin/sh");
        sb.AppendLine("tries=0");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"while kill -0 {pid} 2>/dev/null; do");
        sb.AppendLine("  tries=$((tries + 1))");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  if [ $tries -gt 120 ]; then echo 'failed: the app did not exit' > {result}; rm -f \"$0\"; exit 1; fi");
        sb.AppendLine("  sleep 1");
        sb.AppendLine("done");
        sb.AppendLine("start() {");
        sb.AppendLine(target.EndsWith(".app", StringComparison.Ordinal) ? $"  open {ShQuote(target)}" : $"  nohup {ShQuote(target)} >/dev/null 2>&1 &");
        sb.AppendLine("  rm -f \"$0\"");
        sb.AppendLine("}");
        sb.AppendLine("rollback() {");
        foreach (var f in Enumerable.Reverse(files))
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  if [ -e {Old(f)} ]; then mv -f {Old(f)} {App(f)}; elif [ ! -e {New(f)} ]; then rm -f {App(f)}; fi");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  rm -rf {ShQuote(app + "/" + WorkFolder + "/new")}");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  rmdir {ShQuote(app + "/" + WorkFolder + "/old")} 2>/dev/null");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"  echo failed > {result}");
        sb.AppendLine("  start");
        sb.AppendLine("  exit 1");
        sb.AppendLine("}");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"mkdir -p {ShQuote(app + "/" + WorkFolder + "/old")}");
        foreach (var d in folders)
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"mkdir -p {App(d)} {Old(d)}");
        foreach (var f in files)
        {
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"if [ -e {App(f)} ]; then mv -f {App(f)} {Old(f)} || rollback; fi");
            sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"mv -f {New(f)} {App(f)} || rollback");
        }
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"rm -rf {ShQuote(app + "/" + WorkFolder + "/old")} {ShQuote(app + "/" + WorkFolder + "/new")}");
        sb.AppendLine(System.Globalization.CultureInfo.InvariantCulture, $"echo ok > {result}");
        sb.AppendLine("start");
        return sb.ToString();
    }
}
