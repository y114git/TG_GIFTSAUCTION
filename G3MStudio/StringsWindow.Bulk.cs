using System.Text.RegularExpressions;
using Avalonia.Platform.Storage;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;
using StringUse = G3MLib.DataFile.References.StringUse;

namespace G3MStudio;

public sealed partial class StringsWindow
{
    internal async Task ReplaceAllAsync()
    {
        if (_filter.Text is not { Length: > 0 } needle)
        {
            _note.Text = Loc.Current["strings.replaceNeedsFilter"];
            return;
        }

        try
        {
            var pattern = new Regex(_regex.IsChecked == true ? needle : Regex.Escape(needle), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(2));
            var with = _regex.IsChecked == true ? _replaceWith.Text ?? "" : (_replaceWith.Text ?? "").Replace("$", "$$", StringComparison.Ordinal);
            IReadOnlyList<(int Index, string Text)> changes;
            int risky, held;
            using (_workspace.Lock.Read())
            {
                changes = StringTable.ReplaceChanges(_workspace.Data, Shown.Select(r => r.Index), pattern, with, out held);
                risky = changes.Count(c => IsRisky(c.Index));
            }

            if (changes.Count > 0 && !await ConfirmChangeAsync(changes.Count, risky)) return;
            var done = TryEdit(changes.Count == 0 ? null : StringTable.ToEdit(changes), "strings.noChange");
            if (done) _note.Text = Loc.Current.Format("strings.replaced", ("count", changes.Count));
            if (held > 0 && (done || changes.Count == 0)) _note.Text += " " + Loc.Current.Format("strings.importHeld", ("held", held));
        }
        catch (Exception e) when (e is ArgumentException or RegexMatchTimeoutException)
        {
            _note.Text = e.Message;
        }
    }

    /// <summary>Writes the shown strings as CSV (index, text). A failure is shown in the note; an existing file stays whole until the new one is complete.</summary>
    internal void ExportTo(string path)
    {
        var temp = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temp, StringTable.ToCsv(Shown.Select(r => (r.Index, r.Text))), new System.Text.UTF8Encoding(true));
            File.Move(temp, path, true);
            _note.Text = Loc.Current.Format("strings.exported", ("count", Shown.Count), ("name", Path.GetFileName(path)));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            _note.Text = Loc.Current.Format("strings.exportFailed", ("reason", e.Message));
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    /// <summary>Sets the text of the strings the CSV names by number, as one undo step.</summary>
    internal async Task ImportFromAsync(string path)
    {
        try
        {
            if (new FileInfo(path).Length > MaxCsvBytes)
            {
                _note.Text = Loc.Current.Format("strings.importTooBig", ("limit", MaxCsvBytes / (1024 * 1024)));
                return;
            }

            var rows = StringTable.ParseCsv(await File.ReadAllTextAsync(path));
            IReadOnlyList<(int Index, string Text)> changes;
            int skipped, held, risky;
            using (_workspace.Lock.Read())
            {
                changes = StringTable.ImportChanges(_workspace.Data, rows, out skipped, out held);
                risky = changes.Count(c => IsRisky(c.Index));
            }

            if (changes.Count > 0 && !await ConfirmChangeAsync(changes.Count, risky)) return;
            if (!TryEdit(changes.Count == 0 ? null : StringTable.ToEdit(changes), "strings.noChange")) return;
            _note.Text = Loc.Current.Format("strings.imported", ("count", changes.Count), ("skipped", skipped));
            if (held > 0) _note.Text += " " + Loc.Current.Format("strings.importHeld", ("held", held));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidDataException or OutOfMemoryException)
        {
            _note.Text = Loc.Current.Format("strings.importFailed", ("reason", e is OutOfMemoryException ? Loc.Current.Format("strings.importTooBig", ("limit", MaxCsvBytes / (1024 * 1024))) : e.Message));
        }
    }

    // A name or a text that code compares against can break the game when it changes, so the question says how many of those are in the batch.
    private bool IsRisky(int index)
    {
        var use = IndexCurrent && _index is { } current ? current.ClassifyString(_workspace.Data.Strings[index]) : ResourceNames().Contains(_workspace.Data.Strings[index].Content ?? "") ? StringUse.ResourceName : (StringUse?)null;
        return use is StringUse.ResourceName or StringUse.CodeLiteral;
    }

    private Task<bool> ConfirmChangeAsync(int count, int risky) => ProjectDialogs.AskConfirmAsync(this, Loc.Current["strings.confirmTitle"],
        Loc.Current.Format("strings.confirmChange", ("count", count)) + (risky > 0 ? " " + Loc.Current.Format("strings.confirmRisky", ("risky", risky)) : ""));

    private async Task ExportAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = Loc.Current["strings.export"].TrimEnd('.'),
            DefaultExtension = "csv",
            SuggestedFileName = "string_table.csv",
            FileTypeChoices = [new Avalonia.Platform.Storage.FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
        });
        if (file?.TryGetLocalPath() is { } path) ExportTo(path);
    }

    private async Task ImportAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = Loc.Current["strings.import"].TrimEnd('.'),
            AllowMultiple = false,
            FileTypeFilter = [new Avalonia.Platform.Storage.FilePickerFileType("CSV") { Patterns = ["*.csv"] }],
        });
        if (files.Count == 1 && files[0].TryGetLocalPath() is { } path) await ImportFromAsync(path);
    }
}
