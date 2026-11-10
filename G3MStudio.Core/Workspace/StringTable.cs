using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using G3MLib.DataFile;
using G3MLib.DataFile.Models;

namespace G3MStudio.Core.Workspace;

/// <summary>Adds a new entry at the end of the string table. Undo takes it out again.</summary>
public sealed class StringAddCommand(string text) : EditCommand
{
    public const string Name = "addString";

    private GameMakerString? _added;

    public override string Kind => Name;

    public override string Editor => "strings";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["text"] = text };

    public override IReadOnlyList<ProjectTouch>? Touches => null;

    public static StringAddCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(a["text"]);

    public override void Apply(GameMakerData data)
    {
        // The table holds each text once; MakeString would give back the entry that has it.
        if (data.Strings.Any(s => s.Content == text)) throw new InvalidOperationException("The string table has this text already.");
        _added = data.Strings.MakeString(text, out _);
    }

    public override void Revert(GameMakerData data)
    {
        if (_added is null) throw new InvalidOperationException("The edit was never applied.");
        data.Strings.Remove(_added);
    }
}

/// <summary>Removes one entry of the string table. The caller checks first that nothing uses it. Undo puts it back in the same place.</summary>
public sealed class StringDeleteCommand(int index) : EditCommand
{
    public const string Name = "deleteString";

    private GameMakerString? _removed;

    public override string Kind => Name;

    public override string Editor => "strings";

    public override IReadOnlyDictionary<string, string> Args { get; } = new Dictionary<string, string> { ["index"] = index.ToString(CultureInfo.InvariantCulture) };

    public override IReadOnlyList<ProjectTouch>? Touches => null;

    public static StringDeleteCommand FromArgs(IReadOnlyDictionary<string, string> a) => new(int.Parse(a["index"], CultureInfo.InvariantCulture));

    public override void Apply(GameMakerData data)
    {
        if (index < 0 || index >= data.Strings.Count) throw new InvalidOperationException($"The string table has no entry {index}.");
        _removed = data.Strings[index];
        data.Strings.RemoveAt(index);
    }

    public override void Revert(GameMakerData data)
    {
        if (_removed is null) throw new InvalidOperationException("The edit was never applied.");
        data.Strings.Insert(index, _removed);
    }
}

/// <summary>Whole-table edits and the CSV form of the table: each edit is one group, so one undo takes back the batch.</summary>
public static class StringTable
{
    /// <summary>The rows as CSV with the header <c>index,text</c>: quotes around every text, a quote doubled, line breaks kept inside the quotes.</summary>
    public static string ToCsv(IEnumerable<(int Index, string Text)> rows)
    {
        var csv = new StringBuilder("index,text\r\n");
        foreach (var (index, text) in rows) csv.Append(index.ToString(CultureInfo.InvariantCulture)).Append(",\"").Append(text.Replace("\"", "\"\"", StringComparison.Ordinal)).Append("\"\r\n");
        return csv.ToString();
    }

    /// <summary>Reads CSV in the form <see cref="ToCsv"/> writes (the header line is optional). A line that is not <c>number,text</c> is an error.</summary>
    public static IReadOnlyList<(int Index, string Text)> ParseCsv(string csv)
    {
        var rows = new List<(int, string)>();
        var at = 0;
        var first = true;
        while (at < csv.Length)
        {
            var comma = csv.IndexOf(',', at);
            var head = comma < 0 ? csv[at..].Trim() : csv[at..comma].Trim();
            if (head.Length == 0 && comma < 0) break;
            if (first && head == "index")
            {
                var eol = csv.IndexOf('\n', at);
                at = eol < 0 ? csv.Length : eol + 1;
                first = false;
                continue;
            }

            first = false;
            if (comma < 0 || !int.TryParse(head, NumberStyles.None, CultureInfo.InvariantCulture, out var index)) throw new InvalidDataException($"'{head}' is not a string number.");
            var (text, next) = ReadField(csv, comma + 1);
            rows.Add((index, text));
            at = next;
        }

        return rows;
    }

    private static (string Text, int Next) ReadField(string csv, int at)
    {
        var text = new StringBuilder();
        if (at < csv.Length && csv[at] == '"')
        {
            at++;
            while (true)
            {
                if (at >= csv.Length) throw new InvalidDataException("A quoted text is not closed.");
                if (csv[at] == '"' && at + 1 < csv.Length && csv[at + 1] == '"')
                {
                    text.Append('"');
                    at += 2;
                }
                else if (csv[at] == '"')
                {
                    at++;
                    break;
                }
                else text.Append(csv[at++]);
            }
        }
        else
        {
            while (at < csv.Length && csv[at] is not ('\r' or '\n')) text.Append(csv[at++]);
        }

        while (at < csv.Length && csv[at] is '\r' or '\n') at++;
        return (text.ToString(), at);
    }

    /// <summary>One edit that sets the text of every listed entry that differs. Null when nothing differs. <paramref name="skipped"/> counts numbers outside the table.</summary>
    public static EditCommand? ImportEdits(GameMakerData data, IEnumerable<(int Index, string Text)> rows, out int skipped)
    {
        var edits = new List<EditCommand>();
        skipped = 0;
        foreach (var (index, text) in rows)
        {
            if (index < 0 || index >= data.Strings.Count) skipped++;
            else if (data.Strings[index].Content != text) edits.Add(new EditStringCommand(index, text));
        }

        return edits.Count == 0 ? null : new GroupCommand(edits);
    }

    /// <summary>One edit that replaces every match of the pattern in the listed entries. Null when nothing matches.</summary>
    public static EditCommand? ReplaceEdits(GameMakerData data, IEnumerable<int> indexes, Regex pattern, string replacement)
    {
        var edits = new List<EditCommand>();
        foreach (var index in indexes)
        {
            var old = data.Strings[index].Content ?? "";
            var changed = pattern.Replace(old, replacement);
            if (changed != old) edits.Add(new EditStringCommand(index, changed));
        }

        return edits.Count == 0 ? null : new GroupCommand(edits);
    }
}
