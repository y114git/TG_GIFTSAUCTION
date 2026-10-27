using System;
using System.Collections.Generic;
using System.IO;
using G3MLib.DataFile;
using G3MLib.DataFile.Compiler;
using Xunit;

namespace G3MLib.Tests;

public sealed class GmlProjectCompileTests
{
    private static readonly string Projects = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "GameMakerProjects");

    // Compiles every .gml file of a local GameMaker project as its own script, so one failure does not hide the rest.
    [Fact]
    public void EveryCoverAssaultSourceCompiles()
    {
        string project = Path.Combine(Projects, "Cover Assault");
        Assert.SkipUnless(Directory.Exists(project), "Cover Assault project not found");

        using GameMakerData data = GameMakerData.CreateNew2026LTS();
        List<string> failures = [];
        int count = 0;
        foreach (string file in Directory.GetFiles(project, "*.gml", SearchOption.AllDirectories))
        {
            CodeImportGroup group = new(data) { AutoCreateAssets = false };
            group.QueueReplace($"gml_Script_probe{count++}", File.ReadAllText(file));
            try
            {
                group.Import();
            }
            catch (Exception e)
            {
                failures.Add($"{Path.GetRelativePath(project, file)}: {e.Message.ReplaceLineEndings(" ")}");
            }
        }
        Assert.True(count > 0);
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }
}
