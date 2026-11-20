using G3MLib.Analyzer.Compiler;
using G3MLib.DataFile;
using G3MStudio.Core.GmlDocs;

namespace G3MStudio.Core.Workspace;

/// <summary>A number in code that stands for an asset, and the asset name to write instead.</summary>
public sealed record AssetNumberEdit(int Start, int Length, string Type, string Name);

/// <summary>
/// Which asset type each argument of a function (and each built-in variable) takes. Built-in functions come from the bundled GML reference;
/// the functions of the game itself are learned from the calls that already pass an asset name, so a script such as scr_play(snd_item) teaches that its first argument is a sound.
/// </summary>
public sealed class AssetParameters
{
    private static readonly Dictionary<string, string> TypeOfReference = new(StringComparer.Ordinal)
    {
        ["Asset.GMSprite"] = "sprites",
        ["Asset.GMPath"] = "paths",
        ["Asset.GMObject"] = "objects",
        ["Asset.GMRoom"] = "rooms",
        ["Asset.GMFont"] = "fonts",
        ["Asset.GMShader"] = "shaders",
        ["Asset.GMSound"] = "sounds",
        ["Asset.GMTileSet"] = "tileSets",
        ["Asset.GMParticleSystem"] = "particleSystems",
    };

    // The reference calls these parameters "Function": a number there is a script index only for the functions that take a script asset.
    private static readonly HashSet<string> ScriptFunctions = new(StringComparer.Ordinal) { "script_execute", "script_execute_ext", "script_exists", "script_get_name" };

    private readonly Dictionary<string, string?[]> _functions = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _variables = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Dictionary<int, Dictionary<string, int>>> _votes = new(StringComparer.Ordinal);

    public static AssetParameters Builtin(GmlReference reference)
    {
        var result = new AssetParameters();
        foreach (var entry in reference.Entries)
        {
            if (entry.Kind == RefKind.Variable && entry.Returns is { } returns && TypeOfReference.TryGetValue(returns, out var variableType))
                result._variables[entry.Name] = variableType;
            if (entry.Kind != RefKind.Function) continue;
            var types = new string?[entry.Parameters.Count];
            var any = false;
            for (var i = 0; i < types.Length; i++)
            {
                var parameter = entry.Parameters[i];
                if (parameter.Rest) continue;
                types[i] = TypeOfReference.GetValueOrDefault(parameter.Type) ?? (parameter.Type == "Function" && ScriptFunctions.Contains(entry.Name) ? "scripts" : null);
                any |= types[i] is not null;
            }

            if (any) result._functions[entry.Name] = types;
        }

        return result;
    }

    /// <summary>The asset type the argument takes, or null when it is not an asset (or the function is not known).</summary>
    public string? TypeOf(string function, int argument) => _functions.TryGetValue(function, out var types) && argument >= 0 && argument < types.Length ? types[argument] : null;

    public string? TypeOfVariable(string name) => _variables.GetValueOrDefault(name);

    public bool IsBuiltin(string function) => _functions.ContainsKey(function);

    /// <summary>Counts the calls in <paramref name="text"/> that pass an asset name to a function this table does not know; call <see cref="Seal"/> when every entry was read.</summary>
    public void Learn(string text, AssetNames names)
    {
        var tokens = GmlTokenizer.Tokenize(text, true);
        foreach (var (call, argument, index) in Arguments(tokens, text))
        {
            if (_functions.ContainsKey(call)) continue;
            if (tokens[index].Kind != GmlTokenKind.Identifier || names.TypeOf(text[tokens[index].Span.StartOffset..tokens[index].Span.EndOffset]) is not { } type) continue;
            lock (_votes)
            {
                var arguments = _votes.TryGetValue(call, out var found) ? found : _votes[call] = [];
                var kinds = arguments.TryGetValue(argument, out var counted) ? counted : arguments[argument] = [];
                kinds[type] = kinds.GetValueOrDefault(type) + 1;
            }
        }
    }

    /// <summary>Turns the votes into parameter types: an argument that only ever received one asset type is that type; one that received two is left alone.</summary>
    public void Seal()
    {
        lock (_votes)
        {
            foreach (var (call, arguments) in _votes)
            {
                var types = new string?[arguments.Keys.Max() + 1];
                foreach (var (argument, kinds) in arguments)
                    if (kinds.Count == 1) types[argument] = kinds.Keys.First();
                if (types.Any(t => t is not null)) _functions[call] = types;
            }

            _votes.Clear();
        }
    }

    /// <summary>Every whole argument of a call: the function name, the argument index and the token that is the whole argument.</summary>
    internal static IEnumerable<(string Call, int Argument, int Token)> Arguments(IReadOnlyList<GmlToken> tokens, string text)
    {
        // One frame per open bracket: the function the parentheses belong to (null for other brackets) and the index of the argument being read.
        var frames = new Stack<(string? Call, int Argument)>();
        string Text(int i) => text[tokens[i].Span.StartOffset..tokens[i].Span.EndOffset];
        for (var i = 0; i < tokens.Count; i++)
        {
            var kind = tokens[i].Kind;
            if (kind is not (GmlTokenKind.Separator or GmlTokenKind.Operator))
            {
                if (frames.Count > 0 && frames.Peek().Call is { } call && i > 0 && Text(i - 1) is "(" or "," && i + 1 < tokens.Count && Text(i + 1) is "," or ")"
                    && kind is GmlTokenKind.Number or GmlTokenKind.Identifier)
                    yield return (call, frames.Peek().Argument, i);
                continue;
            }

            switch (Text(i))
            {
                case "(":
                    frames.Push((i > 0 && tokens[i - 1].Kind == GmlTokenKind.Identifier ? Text(i - 1) : null, 0));
                    break;
                case "[" or "{":
                    frames.Push((null, 0));
                    break;
                case ")" or "]" or "}":
                    if (frames.Count > 0) frames.Pop();
                    break;
                case "," when frames.Count > 0:
                    var (name, argument) = frames.Pop();
                    frames.Push((name, argument + 1));
                    break;
            }
        }
    }
}

/// <summary>Asset names by index for each asset type of an open data file, and the type of each name.</summary>
public sealed class AssetNames
{
    private readonly Dictionary<string, string[]> _byType = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _typeOfName = new(StringComparer.Ordinal);

    public AssetNames(GameMakerData data)
    {
        foreach (var type in ResourceTypes.All.Where(t => UsageSearch.AssetTypes.Contains(t.Key)))
        {
            var items = (type.Raw ?? type.Items)(data)?.ToArray() ?? [];
            _byType[type.Key] = [.. items.Select(i => i is null ? "" : ResourceTypes.NameOf(i))];
            foreach (var name in _byType[type.Key])
                if (name.Length > 0) _typeOfName.TryAdd(name, type.Key);
        }
    }

    public string? NameAt(string type, long index) => _byType.TryGetValue(type, out var names) && index >= 0 && index < names.Length && names[(int)index].Length > 0 ? names[(int)index] : null;

    public string? TypeOf(string name) => _typeOfName.GetValueOrDefault(name);
}

/// <summary>Finds the numbers in code that are really assets (draw_sprite(5, ...), sprite_index = 12) and writes the asset names instead.</summary>
public static class AssetNumberReplacer
{
    /// <summary>The replacements for one code text. <paramref name="allowed"/> limits the asset types; null allows all.</summary>
    public static IReadOnlyList<AssetNumberEdit> Find(string text, AssetNames names, AssetParameters parameters, Func<string, bool>? allowed = null)
    {
        var edits = new List<AssetNumberEdit>();
        var tokens = GmlTokenizer.Tokenize(text, true);
        void Consider(int token, string? type)
        {
            if (type is null || allowed?.Invoke(type) == false || tokens[token].Kind != GmlTokenKind.Number) return;
            var (start, end) = (tokens[token].Span.StartOffset, tokens[token].Span.EndOffset);
            var word = text.AsSpan(start, end - start);
            // Only a plain whole number: not hex, not a real, not an expression part.
            if (!long.TryParse(word, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var index)) return;
            if (names.NameAt(type, index) is { } name) edits.Add(new AssetNumberEdit(start, end - start, type, name));
        }

        foreach (var (call, argument, token) in AssetParameters.Arguments(tokens, text)) Consider(token, parameters.TypeOf(call, argument));
        // sprite_index = 5;  room == 3  (the number is the whole right side)
        for (var i = 2; i + 1 < tokens.Count; i++)
        {
            if (tokens[i].Kind != GmlTokenKind.Number || tokens[i - 1].Kind != GmlTokenKind.Operator || tokens[i - 2].Kind != GmlTokenKind.Identifier) continue;
            var op = text[tokens[i - 1].Span.StartOffset..tokens[i - 1].Span.EndOffset];
            var next = text[tokens[i + 1].Span.StartOffset..tokens[i + 1].Span.EndOffset];
            if (op is not ("=" or "==" or "!=") || next is not (";" or ")" or "," or "&&" or "||" or "}")) continue;
            var variable = text[tokens[i - 2].Span.StartOffset..tokens[i - 2].Span.EndOffset];
            Consider(i, parameters.TypeOfVariable(variable));
        }

        return [.. edits.DistinctBy(e => e.Start).OrderBy(e => e.Start)];
    }

    public static string Apply(string text, IReadOnlyList<AssetNumberEdit> edits)
    {
        var result = new System.Text.StringBuilder(text);
        foreach (var edit in edits.OrderByDescending(e => e.Start)) result.Remove(edit.Start, edit.Length).Insert(edit.Start, edit.Name);
        return result.ToString();
    }
}
