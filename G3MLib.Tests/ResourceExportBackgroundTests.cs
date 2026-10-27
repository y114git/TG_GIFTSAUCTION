using System;
using System.IO;
using G3MLib.DataFile;
using G3MLib.Modding.Resources;
using Xunit;

namespace G3MLib.Tests;

public sealed class ResourceExportBackgroundTests
{
    [Fact]
    public void BackgroundAndTilesetExportSkipEmptySlots()
    {
        string directory = Path.Combine(Path.GetTempPath(), $"G3MLib-{Guid.NewGuid():N}");
        try
        {
            using FileStream input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "data-2026.win"));
            using GameMakerData data = GameMakerIO.Read(input);
            Assert.Contains(data.Backgrounds, background => background is null);
            ResourceExportService.ExportTypes(data, directory, "", ["Backgrounds", "Tilesets"]);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
