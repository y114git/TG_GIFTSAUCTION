using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

public sealed class ModsTests
{
    [Fact]
    public async Task ThePatchCommandsCreateValidateAndApplyAPatchOnFiles()
    {
        using var dir = new TempDir();
        var original = TestData.WriteSample(dir, "original.win", 2, true);
        var modified = TestData.WriteSample(dir, "modified.win", 3, true);
        var patch = dir.File("mod.g3mpatch");
        var patched = dir.File("patched.win");
        var answers = new Queue<string>([original, patch]);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(modified);
                window.FilePicker = (_, _) => Task.FromResult<string?>(answers.Dequeue());
                await window.CreatePatchAsync();
                Assert.True(File.Exists(patch), string.Join("; ", window.Notifications.Active.Select(n => n.Text)));

                answers.Enqueue(patch);
                await window.ValidatePatchAsync();
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("valid patch", StringComparison.Ordinal));

                await window.CloseWorkspaceAsync();
                foreach (var next in new[] { original, patch, patched }) answers.Enqueue(next);
                await window.ApplyPatchAsync();
                Assert.True(File.Exists(patched), string.Join("; ", window.Notifications.Active.Select(n => n.Text)));
                using var patchedData = await G3MLib.DataFile.GameMakerIO.ReadAsync(patched, cancellationToken: TestContext.Current.CancellationToken);
                Assert.Equal(3, patchedData.GameObjects.Count);

                // The output may not overwrite the file the patch is applied to.
                foreach (var next in new[] { original, patch, original }) answers.Enqueue(next);
                await window.ApplyPatchAsync();
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("different file", StringComparison.Ordinal));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TwoPatchesFromTheSameOriginalMergeIntoOne()
    {
        using var dir = new TempDir();
        var original = TestData.WriteSample(dir, "original.win", 2, true);
        var first = TestData.WriteSample(dir, "first.win", 3, true);
        var second = TestData.WriteSample(dir, "second.win", 4, true);
        var merged = dir.File("merged.g3mpatch");
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var patches = new List<string>();
                foreach (var (modified, name) in new[] { (first, "a.g3mpatch"), (second, "b.g3mpatch") })
                {
                    var result = await G3MLib.Modding.Patching.PatchService.CreatePatchAsync(original, modified, dir.File(name), cancellationToken: TestContext.Current.CancellationToken);
                    Assert.True(result.Success, result.Error);
                    patches.Add(dir.File(name));
                }

                window.FilePicker = (_, _) => Task.FromResult<string?>(Path.GetFileName(original) == "original.win" ? merged : null);
                window.FilesPicker = _ => Task.FromResult<IReadOnlyList<string>>(patches);
                await window.OpenAsync(original);
                await window.MergePatchesAsync();
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("Merged patch saved", StringComparison.Ordinal) || n.Text.Contains("failed", StringComparison.Ordinal));
                Assert.True(File.Exists(merged), string.Join("; ", window.Notifications.Active.Select(n => n.Text)));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TwoDataFilesCompareIntoAReport()
    {
        using var dir = new TempDir();
        var original = TestData.WriteSample(dir, "original.win", 2, true);
        var changed = TestData.WriteSample(dir, "changed.win", 3, true);
        var report = dir.File("report.txt");
        var answers = new Queue<string>([original, changed, report]);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                window.FilePicker = (_, _) => Task.FromResult<string?>(answers.Dequeue());
                await window.CompareFilesAsync();
                Assert.True(File.Exists(report), string.Join("; ", window.Notifications.Active.Select(n => n.Text)));
                Assert.Contains(window.Notifications.Active, n => n.Text.Contains("differences", StringComparison.Ordinal));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task TheModZipHoldsAPatchAndAConfigThatPointsAtIt()
    {
        using var dir = new TempDir();
        var original = TestData.WriteSample(dir, "original.win", 2, true);
        var modified = TestData.WriteSample(dir, "modified.win", 3, true);
        var zip = dir.File("mod.zip");
        var answers = new Queue<string>([original, zip]);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(modified);
                window.FilePicker = (_, _) => Task.FromResult<string?>(answers.Dequeue());
                await window.ExportModAsync();
                await using var archive = await System.IO.Compression.ZipFile.OpenReadAsync(zip, TestContext.Current.CancellationToken);
                Assert.NotNull(archive.GetEntry("mod.g3mpatch"));
                using var reader = new StreamReader(await archive.GetEntry("mod_config.json")!.OpenAsync(TestContext.Current.CancellationToken));
                var config = System.Text.Json.JsonDocument.Parse(await reader.ReadToEndAsync(TestContext.Current.CancellationToken)).RootElement;
                Assert.Equal("2.0.0", config.GetProperty("config_version").GetString());
                Assert.Equal("patch", config.GetProperty("files")[0].GetProperty("type").GetString());
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ModIdsAreLowercaseWordsThatStartWithALetter()
    {
        Assert.Equal("my_cool_mod", ModPackage.MakeId("My Cool Mod!"));
        Assert.Equal("mod_1st", ModPackage.MakeId("1st"));
    }
}
