using G3MLib.Analyzer.Compiler;
using G3MStudio.Core.GmlDocs;
using G3MStudio.Core.Workspace;
using G3MStudio.Localization;

namespace G3MStudio.Editor;

// The info card of the hover for names (functions, variables, constants, assets, keywords), built once per name position, edit step, language, nicknames and target.
public sealed partial class CodeEditorTab
{
    private readonly Dictionary<int, string?> _cards = [];
    private (int Text, int History, string Language, string Target, int Nicknames) _cardStamp = (-1, -1, "", "", -1);

    private string? CachedCard(Workspace workspace, GmlDoc doc, int token)
    {
        var stamp = (_textVersion, workspace.History.Version, Loc.Current.Language, Target, workspace.Nicknames.Version);
        if (stamp != _cardStamp)
        {
            _cards.Clear();
            _cardStamp = stamp;
        }

        var key = doc.Start(token);
        if (!_cards.TryGetValue(key, out var card)) _cards[key] = card = Card(workspace, doc, token);
        return card;
    }

    // The uses are counted in this code entry only; counting in all code would decompile every entry on a hover.
    private string? Card(Workspace workspace, GmlDoc doc, int token)
    {
        var typed = doc.Slice(token);
        if (doc.Tokens[token].Kind == GmlTokenKind.Keyword) return GmlHover.Card(Loc.Current["code.hover.keyword"], typed);
        // A nickname hovers like the name of its resource.
        var word = GmlNames.Real(workspace, typed);
        var uses = GmlSymbols.References(doc, word).Count;
        string Uses() => uses > 0 ? "\n" + Loc.Current.Format("code.hover.uses", ("count", uses)) : "";
        if (GameFunctions.FromDoc(doc, word, Target) is { } declared) return GmlHelp.Describe(declared, word, workspace.Nicknames.CodeLabel) + Uses();
        var signature = GmlNames.SignatureOf(workspace, word);
        if (signature?.Doc is { } documented) return GmlHelp.Describe(documented, word, workspace.Nicknames.CodeLabel) + (documented.Game ? Uses() : "");
        if (signature is not null) return Loc.Current["code.tag.builtin"] + " " + signature.Label;
        if (GmlNames.IsGameFunction(workspace, word) && GameFunctions.Find(workspace, word) is { } known) return GmlHelp.Describe(known, word, workspace.Nicknames.CodeLabel) + Uses();

        if (GmlSymbols.DeclarationOf(doc, word) is var declaration and >= 0)
            return GmlHover.Card(Loc.Current["code.hover.localVariable"], word, GmlHover.ValueType(doc, declaration), DocComment.Before(doc.Text, doc.Start(declaration))?.Summary, Loc.Current.Format("code.hover.line", ("line", GmlHover.LineOf(doc, declaration))), uses);
        var named = GmlNames.For(workspace)(word);
        if (named is "Variable" or "Constant" && GmlNames.Reference.Find(word) is { } entry) return GmlHelp.Describe(entry, word);
        if (named == "Asset") return GmlHover.Card(Loc.Current["code.hover.asset"], GmlNames.Definitions(workspace)(word) is { } assetKey && workspace.Nicknames.Of(assetKey) is { } nick ? $"{nick} ({word})" : word, where: GmlNames.Definitions(workspace)(word) is { } key ? GmlHover.Place(key, workspace.Nicknames) : null, uses: uses);
        if (named is not null) return GmlHover.Card(Loc.Current[named == "Constant" ? "code.hover.builtinConstant" : "code.hover.builtinVariable"], word, uses: uses);

        var global = token > 1 && doc.IsPunct(token - 1, '.') && doc.Slice(token - 2) == "global";
        var assigned = GmlHover.FirstAssignment(doc, word, global);
        if (!global && (token > 0 && doc.IsPunct(token - 1, '.') || assigned < 0)) return null;
        var kind = Loc.Current[global ? "code.hover.globalVariable" : "code.hover.variable"];
        return assigned < 0
            ? GmlHover.Card(kind, word, uses: uses)
            : GmlHover.Card(kind, word, GmlHover.ValueType(doc, assigned), DocComment.Before(doc.Text, doc.Start(assigned))?.Summary, Loc.Current.Format("code.hover.line", ("line", GmlHover.LineOf(doc, assigned))), uses);
    }
}
