using System.CommandLine;
using System.Diagnostics;
using G3MToolCLI.Services.Execution;

namespace G3MToolCLI.Commands;

public static class ExecuteCommand
{
    public static Command Create()
    {
        var command = new Command("execute", "Execute .csx scripts, external programs, or xdelta commands.\n  Usage: execute <target> [options] -- [args]\n  Examples:\n    execute script.csx --data data.win --output patched.win\n    execute export.csx --data data.win --output exports/\n    execute xdelta --xdelta-path ./xdelta -- -d -s original.win patch.xdelta output.win");
        var targetArg = new Argument<string>("target") { Description = "Program, script (.csx), or 'xdelta' to execute" };
        var argsArg = new Argument<string[]>("args") { DefaultValueFactory = _ => [], Description = "Arguments to pass" };
        var dataOption = new Option<FileInfo?>("--data", ["-d"]) { Description = "Path to data file (.win/.ios/.droid/.unx) (optional for .csx scripts)" };
        var outputOption = new Option<string?>("--output", ["-o"]) { Description = "Script output. A folder (existing, or ending with a path separator) receives the script's exports. A file path also saves the data file there. Default: exports go to the current folder and the data file is not saved." };
        var inputOption = new Option<DirectoryInfo?>("--input", ["-i"]) { Description = "Input directory for scripts (e.g., sprites folder for ImportSprites)" };
        var timeoutOption = new Option<int?>("--timeout") { Description = "Stop a .csx script after this many seconds" };
        timeoutOption.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() is <= 0) result.AddError("--timeout must be a positive number of seconds.");
        });
        command.Add(targetArg);
        command.Add(argsArg);
        command.Add(dataOption);
        command.Add(outputOption);
        command.Add(inputOption);
        command.Add(timeoutOption);
        command.SetAction(async parseResult =>
        {
            var target = parseResult.GetValue(targetArg)!;
            var args = parseResult.GetValue(argsArg) ?? [];
            var data = parseResult.GetValue(dataOption);
            var output = parseResult.GetValue(outputOption);
            var input = parseResult.GetValue(inputOption);
            var timeout = parseResult.GetValue(timeoutOption) is { } seconds ? TimeSpan.FromSeconds(seconds) : (TimeSpan?)null;
            if (target.Equals("xdelta", StringComparison.OrdinalIgnoreCase))
            {
                await ExecuteXDeltaAsync(args);
            }
            else if (target.EndsWith(".csx", StringComparison.OrdinalIgnoreCase))
            {
                var exportsOnly = !string.IsNullOrWhiteSpace(output) && IsDirectoryTarget(output);
                var savePath = !string.IsNullOrWhiteSpace(output) && !exportsOnly ? Path.GetFullPath(output) : null;
                var exportDirectory = exportsOnly ? Path.GetFullPath(output!) : null;
                var finalArgs = input is null ? args : [input.FullName, .. args];
                var scriptResult = await ExecuteService.ExecuteScriptAsync(target, data?.FullName, savePath, finalArgs, exportDirectory, timeout, Program.Cancellation);
                if (!scriptResult.Success)
                {
                    await Console.Error.WriteLineAsync("Error: " + scriptResult.Error);
                    Environment.ExitCode = 1;
                }
            }
            else
            {
                Environment.ExitCode = await ExecuteExternalProgramAsync(target, args);
            }
        });
        return command;
    }

    private static bool IsDirectoryTarget(string path) => Directory.Exists(path) || path.EndsWith('/') || path.EndsWith(Path.DirectorySeparatorChar);

    private static async Task ExecuteXDeltaAsync(string[] args)
    {
        string? error;
        string? text = null;
        try
        {
            var result = await EmbeddedXDeltaPathProvider.CreateService().ExecuteRawAsync(args);
            error = result.Success ? null : result.Error;
            text = result.Output;
        }
        catch (InvalidOperationException exception)
        {
            error = exception.Message;
        }
        if (error is not null)
        {
            PatchCommand.WriteErrorJsonOrText("execute xdelta", error);
            Environment.ExitCode = 1;
        }
        else if (Program.JsonOutput)
        {
            PatchCommand.WriteJson(new { success = true, command = "execute xdelta", output = text, warnings = Array.Empty<string>() });
        }
        else if (!string.IsNullOrEmpty(text))
        {
            await Console.Out.WriteAsync(text);
        }
    }

    private static async Task<int> ExecuteExternalProgramAsync(string program, string[] args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = program,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }
            using var process = Process.Start(startInfo);
            if (process == null)
            {
                await Console.Error.WriteLineAsync("Failed to start process: " + program);
                return 1;
            }
#pragma warning disable CA2025 // both copies are awaited below, before the streams are disposed
            await using var standardOutput = Console.OpenStandardOutput();
            await using var standardError = Console.OpenStandardError();
            var outputTask = process.StandardOutput.BaseStream.CopyToAsync(standardOutput);
            var errorTask = process.StandardError.BaseStream.CopyToAsync(standardError);
            try
            {
                await process.WaitForExitAsync(Program.Cancellation);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw;
            }
            await Task.WhenAll(outputTask, errorTask);
#pragma warning restore CA2025
            return process.ExitCode;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await Console.Error.WriteLineAsync("Error executing " + program + ": " + ex.Message);
            return 1;
        }
    }
}
