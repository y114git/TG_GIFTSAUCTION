using System;
using System.IO;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;
using G3MLib.Modding.Resources;
using Xunit;

namespace G3MLib.Tests;

public sealed class ProjectExtensionImportTests
{
    [Fact]
    public void ExtensionCreatedFromExportedFolderCanBeWritten()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"G3MLib-{Guid.NewGuid():N}");
        try
        {
            using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "data-2022.win"));
            using GameMakerData source = GameMakerIO.Read(input);
            Assert.NotEmpty(source.Extensions);
            ResourceExportService.ExportTypes(source, directory, "", ["Extensions"]);

            using GameMakerData target = GameMakerData.CreateNew2026LTS();
            Assert.True(ResourceImportService.Import("Extensions", target, Path.Combine(directory, "Extensions")));
            Assert.NotEmpty(target.Extensions);
            GameMakerIO.Write(new MemoryStream(), target);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
