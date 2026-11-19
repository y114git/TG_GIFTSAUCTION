using G3MLib.DataFile;
using G3MLib.DataFile.Project;
using G3MLib.Modding.Resources;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Core.Projects;

/// <summary>
/// Turns a UndertaleModTool 0.9 project into a G3MStudio project. The UTMT project is a change set over a data file: the library applies it to
/// that file in memory (the same import UTMT runs), then the result is written as a full project and as a data file in <c>build</c>, and the
/// project is registered for that data file so opening it finds the project. The UTMT folder is only read.
/// </summary>
public sealed partial class UtmtProjectImporter(ProjectService projects, IReadOnlyDictionary<string, string>? gitEnvironment = null)
{
    public async Task<UtmtImportReport> ImportAsync(UtmtImportRequest request, IProgress<string>? stage = null, IProgress<BatchProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var info = Inspect(request.ProjectPath);
        var baseFile = Path.GetFullPath(request.BaseDataFile);
        if (!File.Exists(baseFile)) throw new UtmtImportException($"The data file \"{baseFile}\" does not exist.");
        if (info.Scripts.Count > 0 && !request.RunScripts) throw new UtmtImportException($"The project runs scripts ({string.Join(", ", info.Scripts)}). Allow the scripts to convert it, or remove them from the project options.");
        var destination = Path.GetFullPath(request.Destination);
        var before = CheckDestination(info, baseFile, destination);
        var name = ProjectService.SafeName(info.Name);
        var buildFolder = Path.Combine(destination, "build");
        var dataFile = Path.Combine(buildFolder, Path.GetFileName(baseFile));
        var scratch = Directory.CreateTempSubdirectory("g3mstudio-utmt-").FullName;
        try
        {
            stage?.Report("read");
            using var data = await GameMakerIO.ReadAsync(baseFile, NoProgress.Instance, cancellationToken);
            stage?.Report("import");
            var context = ProjectContext.CreateWithDirectories(Path.GetDirectoryName(baseFile) ?? ".", scratch, info.MainFile);
            context.AllowScripts = request.RunScripts;
            // The scratch folder takes what the project writes next to the data file (audio groups, external files); nothing is backed up or restored.
            await Task.Run(() => context.Import(data, new GameFileNoOpBackup()), cancellationToken);
            stage?.Report("write");
            Directory.CreateDirectory(buildFolder);
            await GameMakerIO.WriteAsync(dataFile, data, NoProgress.Instance, cancellationToken);
            foreach (var file in Directory.EnumerateFiles(scratch, "*", SearchOption.AllDirectories))
            {
                var target = Path.Combine(buildFolder, Path.GetRelativePath(scratch, file));
                Directory.CreateDirectory(Path.GetDirectoryName(target) ?? buildFolder);
                File.Move(file, target, true);
            }

            stage?.Report("export");
            // The write left its buffers behind: they go before the export allocates its own.
            await Task.Run(() => ProcessMemory.Reclaim(false), CancellationToken.None);
            var exported = await Task.Run(() => G3MProjectService.Export(data, destination, dataFile, new G3MProjectOptions { Name = name }, progress, cancellationToken), cancellationToken);
            KeepCodeSources(info.Folder, destination);
            var warnings = Notes(info, context.Warnings, exported.Warnings);
            var identity = await DataFileIdentity.ComputeAsync(dataFile, cancellationToken);
            stage?.Report("git");
            var git = await SetUpGitAsync(destination, request.InitGit, info.Name, warnings, cancellationToken);
            // Registered last: a failure or cancel before this point removes the folder and leaves no record behind.
            projects.Adopt(destination, name, identity);
            return new UtmtImportReport(info, destination, Path.Combine(destination, name + G3MProjectService.FileExtension), dataFile, exported.Resources, git, warnings);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            RemoveNew(destination, before);
            if (e is UtmtImportException) throw;
            if (e is ProjectException or System.Text.Json.JsonException or IOException or UnauthorizedAccessException or InvalidDataException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException)
                throw new UtmtImportException(e.Message, e);
            throw;
        }
        catch (OperationCanceledException)
        {
            RemoveNew(destination, before);
            throw;
        }
        finally
        {
            CopyPackage.TryDelete(scratch);
        }
    }
}
