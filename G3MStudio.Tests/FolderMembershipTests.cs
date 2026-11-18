using System.Text.Json;
using G3MStudio.Core.Settings;
using G3MStudio.Core.Workspace;

namespace G3MStudio.Tests;

/// <summary>A resource can be listed in several folders: a folder holds a reference, never a copy.</summary>
public sealed class FolderMembershipTests
{
    private static string Key(string name) => ResourceTypes.KeyOf("objects", name);

    private static (Organizer O, string A, string B, string C) Folders()
    {
        var (o, a) = Organizer.Empty.AddFolder("A", null);
        (o, var b) = o.AddFolder("B", null);
        (o, var c) = o.AddFolder("C", null);
        return (o, a!, b!, c!);
    }

    [Fact]
    public void CopyListsTheKeyInASecondFolderAndKeepsTheFirst()
    {
        var (o, a, b, _) = Folders();
        o = o.MoveTo([Key("obj_0")], a).CopyTo([Key("obj_0")], b);
        Assert.Equal([a, b], o.FoldersOfKey(Key("obj_0")));
        Assert.Contains(Key("obj_0"), o.MembersOf(a));
        Assert.Contains(Key("obj_0"), o.MembersOf(b));
        Assert.Equal([Key("obj_0")], o.InSeveralFolders([Key("obj_0"), Key("obj_1")]));
        Assert.Equal([a, b], o.CopyTo([Key("obj_0")], b).FoldersOfKey(Key("obj_0"))); // twice into one folder changes nothing
    }

    [Fact]
    public void CopyOfAKeyAtTheTopJustFilesIt()
    {
        var (o, a, _, _) = Folders();
        o = o.CopyTo([Key("obj_0")], a);
        Assert.Equal([a], o.FoldersOfKey(Key("obj_0")));
        Assert.Empty(o.InSeveralFolders([Key("obj_0")]));
    }

    [Fact]
    public void RemoveFromLeavesTheOtherFoldersAndThePromotedOneBecomesTheMain()
    {
        var (o, a, b, c) = Folders();
        o = o.MoveTo([Key("obj_0")], a).CopyTo([Key("obj_0")], b).CopyTo([Key("obj_0")], c);
        o = o.RemoveFrom([Key("obj_0")], a);
        Assert.Equal([b, c], o.FoldersOfKey(Key("obj_0")));
        Assert.Equal(b, o.FolderOf[Key("obj_0")]);
        o = o.RemoveFrom([Key("obj_0")], b).RemoveFrom([Key("obj_0")], c);
        Assert.Empty(o.FoldersOfKey(Key("obj_0")));
        Assert.False(o.FolderOf.ContainsKey(Key("obj_0")));
        Assert.Empty(o.AlsoIn);
    }

    [Fact]
    public void MoveToTakesTheKeyOutOfEveryOtherFolder()
    {
        var (o, a, b, c) = Folders();
        o = o.MoveTo([Key("obj_0")], a).CopyTo([Key("obj_0")], b).MoveTo([Key("obj_0")], c);
        Assert.Equal([c], o.FoldersOfKey(Key("obj_0")));
        Assert.Empty(o.AlsoIn);
    }

    [Fact]
    public void CutAndPasteReplacesOnlyTheFolderItWasCutFrom()
    {
        var (o, a, b, c) = Folders();
        o = o.MoveTo([Key("obj_0")], a).CopyTo([Key("obj_0")], b);
        o = o.MoveFrom([Key("obj_0")], a, c);
        Assert.Equal([c, b], o.FoldersOfKey(Key("obj_0")));
        o = o.MoveFrom([Key("obj_0")], c, null); // pasted at the top: it only leaves the folder
        Assert.Equal([b], o.FoldersOfKey(Key("obj_0")));
        o = o.MoveFrom([Key("obj_1")], null, a); // cut from a list that is no folder
        Assert.Equal([a], o.FoldersOfKey(Key("obj_1")));
    }

    [Fact]
    public void EntriesCutFromTwoFoldersAtOnceAllLeaveTheirOwnFolder()
    {
        var (o, a, b, c) = Folders();
        o = o.MoveTo([Key("obj_0")], a).MoveTo([Key("obj_1")], b);
        o = o.MoveFrom([(Key("obj_0"), (string?)a), (Key("obj_1"), b)], c);
        Assert.Equal([c], o.FoldersOfKey(Key("obj_0")));
        Assert.Equal([c], o.FoldersOfKey(Key("obj_1")));
    }

    [Fact]
    public void DeletingAFolderMovesEveryMemberUpWithoutDuplicates()
    {
        var (o, parent) = Organizer.Empty.AddFolder("P", null);
        (o, var child) = o.AddFolder("Child", parent);
        (o, var other) = o.AddFolder("Other", null);
        o = o.MoveTo([Key("obj_0")], child).CopyTo([Key("obj_0")], other!).CopyTo([Key("obj_0")], parent!);
        o = o.DeleteFolder(child!);
        Assert.Equal([parent!, other!], o.FoldersOfKey(Key("obj_0")));
    }

    [Fact]
    public void RenamingAResourceCarriesEveryFolderAndDeletingItDropsThem()
    {
        var (o, a, b, _) = Folders();
        o = o.MoveTo([Key("obj_0")], a).CopyTo([Key("obj_0")], b);
        var renamed = o.Rekey(Key("obj_0"), Key("obj_new"));
        Assert.Equal([a, b], renamed.FoldersOfKey(Key("obj_new")));
        Assert.Empty(renamed.FoldersOfKey(Key("obj_0")));
        var gone = o.Rekey(Key("obj_0"), null);
        Assert.Empty(gone.AlsoIn);
        Assert.Equal([a, b], gone.Restore(Key("obj_0"), a, [], [b]).FoldersOfKey(Key("obj_0")));
    }

    [Fact]
    public void SavedFoldersWrittenBeforeSeveralFoldersExistedStillLoad()
    {
        // The shape the old version wrote: no AlsoIn at all.
        const string old = """{"Folders":[{"Id":"f1","Name":"Old","Parent":null,"Colour":""}],"FolderOf":{"objects:obj_0":"f1"},"Collections":[],"TagColours":{},"FolderView":null}""";
        var loaded = JsonSerializer.Deserialize<Organizer>(old)!.Sanitized();
        Assert.Equal(["f1"], loaded.FoldersOfKey("objects:obj_0"));
        Assert.Empty(loaded.AlsoIn);

        var (o, a, b, _) = Folders();
        o = o.MoveTo([Key("obj_0")], a).CopyTo([Key("obj_0")], b);
        var back = JsonSerializer.Deserialize<Organizer>(JsonSerializer.Serialize(o))!.Sanitized();
        Assert.Equal([a, b], back.FoldersOfKey(Key("obj_0")));
    }

    [Fact]
    public void ADanglingFurtherFolderIsDroppedWhenLoading()
    {
        var o = new Organizer
        {
            Folders = [new VirtualFolder("a", "A", null)],
            FolderOf = new Dictionary<string, string> { ["k"] = "a" },
            AlsoIn = new Dictionary<string, IReadOnlyList<string>> { ["k"] = ["gone", "a"], ["orphan"] = ["a"] },
        }.Sanitized();
        Assert.Empty(o.AlsoIn);
    }

    [Fact]
    public void TheSameEntryListedInTwoFoldersGivesTwoDistinctRows()
    {
        var (o, a, b, _) = Folders();
        o = o.MoveTo([Key("obj_0")], a).CopyTo([Key("obj_0")], b) with { FolderView = true };
        var model = new ExplorerModel(key => key);
#pragma warning disable CA2000 // the model reads the data only while loading
        model.Load(TestData.Create(3));
#pragma warning restore CA2000
        model.Organizer = o;
        var rows = model.Rows.Where(r => r is { IsItem: true, Text: "obj_0" }).ToList();
        Assert.Equal(2, rows.Count);
        Assert.NotEqual(rows[0], rows[1]);
        Assert.Equal(2, rows.Select(r => r.Holder).Distinct().Count());
    }

    [Fact]
    public void TheMultiFolderWarningIsAPromptThatCanBeSilencedButNeverRefused()
    {
        var s = new AppSettings();
        Assert.Equal(PromptChoice.Ask, Prompts.Get(s, Prompts.MultiFolder));
        s = Prompts.Set(s, Prompts.MultiFolder, PromptChoice.Yes);
        Assert.Equal(PromptChoice.Yes, Prompts.Get(s, Prompts.MultiFolder));
        Assert.Equal(s, Prompts.Set(s, Prompts.MultiFolder, PromptChoice.No));
    }

    [Fact]
    public async Task TheWarningAppearsOnlyWhenAResourceGetsASecondFolderAndNeverAgainOnceSilenced()
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                var (o, a, b, c) = Folders();
                window.Explorer.Model.Organizer = o;
                var asked = 0;
                window.Confirmer = (_, _) =>
                {
                    asked++;
                    return Task.FromResult(true);
                };
                var keys = new[] { Key("obj_0") };
                await window.ChangeFoldersAsync(keys, x => x.CopyTo(keys, a)); // first folder: no warning
                Assert.Equal(0, asked);
                await window.ChangeFoldersAsync(keys, x => x.CopyTo(keys, b)); // second folder: warning
                Assert.Equal(1, asked);
                await window.ChangeFoldersAsync(keys, x => x.CopyTo(keys, c)); // already in several: no new situation
                Assert.Equal(1, asked);
                Assert.Equal([a, b, c], window.Explorer.Model.Organizer.FoldersOfKey(keys[0]));

                window.Confirmer = (_, _) => Task.FromResult(false); // declined: nothing changes
                var other = new[] { Key("obj_1") };
                await window.ChangeFoldersAsync(other, x => x.CopyTo(other, a));
                await window.ChangeFoldersAsync(other, x => x.CopyTo(other, b));
                Assert.Equal([a], window.Explorer.Model.Organizer.FoldersOfKey(other[0]));

                window.Workspaces.Settings.Update(x => Prompts.Set(x, Prompts.MultiFolder, PromptChoice.Yes));
                window.Confirmer = (_, _) => throw new InvalidOperationException("must not ask");
                await window.ChangeFoldersAsync(other, x => x.CopyTo(other, b));
                Assert.Equal([a, b], window.Explorer.Model.Organizer.FoldersOfKey(other[0]));
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public async Task ADeclinedPasteKeepsTheCutEntriesForAnotherTry()
    {
        using var dir = new TempDir();
        var path = TestData.WriteSample(dir, objects: 3);
        await HeadlessApp.Run(async () =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            try
            {
                await window.OpenAsync(path);
                var (o, a, b, _) = Folders();
                window.Explorer.Model.Organizer = o.MoveTo([Key("obj_0")], a) with { FolderView = false }; // the folders under Quick Access, every object in its type list
                if (window.Explorer.Model.Rows.FirstOrDefault(r => r.Key == ExplorerModel.QuickFolders) is { Expanded: false }) window.Explorer.Model.Toggle(ExplorerModel.QuickFolders);
                if (!window.Explorer.Model.Rows.First(r => r is { Kind: ExplorerRowKind.Type, Key: "objects" }).Expanded) window.Explorer.Model.Toggle("objects");
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var list = Avalonia.LogicalTree.LogicalExtensions.GetLogicalDescendants(window.Explorer).OfType<Avalonia.Controls.ListBox>().Single(l => l.Name == "List");
                // Cut from the objects list, not from folder A: the paste lists it in a second folder, which asks first.
                list.SelectedItem = window.Explorer.Model.Rows.Single(r => r is { IsItem: true, Text: "obj_0" } && window.Explorer.ParentOf(r)?.Kind == ExplorerRowKind.Type);
                window.CutToFolder();
                list.SelectedItem = window.Explorer.Model.Rows.Single(r => r.Key == ExplorerModel.FolderPrefix + b);
                var asked = 0;
                window.Confirmer = (_, _) =>
                {
                    asked++;
                    return Task.FromResult(asked > 1);
                };
                window.PasteIntoFolder();
                Assert.Equal(1, asked);
                Assert.Equal([a], window.Explorer.Model.Organizer.FoldersOfKey(Key("obj_0")));
                window.PasteIntoFolder();
                Assert.Equal(2, asked);
                Assert.Equal([a, b], window.Explorer.Model.Organizer.FoldersOfKey(Key("obj_0")));
            }
            finally { window.Close(); }
        });
    }
}
