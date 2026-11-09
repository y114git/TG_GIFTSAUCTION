namespace G3MStudio.Core.Settings;

/// <summary>What Studio does at a prompt: ask every time, or answer for the user without asking.</summary>
public enum PromptChoice
{
    Ask,
    Yes,
    No,
}

/// <param name="Id">Stable id, also the key in settings.json.</param>
/// <param name="CanSayNo">False when only "ask" and "always yes" make sense (the prompt picks between two actions, not between doing and not doing).</param>
public sealed record PromptDef(string Id, bool CanSayNo = true);

/// <summary>
/// The one list of prompts that offer "Don't ask again". The Prompts page of Settings lists every entry; a dialog writes its choice here and reads it before it asks.
/// Security prompts (a run file, a plugin, a mod pack, a patch, a script, a download, an update) are not in the list on purpose: they cannot be silenced.
/// </summary>
public static class Prompts
{
    public const string CloseUnsaved = "closeUnsaved";
    public const string TempRunWarning = "tempRunWarning";
    public const string RiskyAction = "riskyAction";
    public const string UnsavedChanges = "unsavedChanges";
    public const string CopyToSave = "copyToSave";
    public const string CopyKeepOrganize = "copyKeepOrganize";
    public const string DeleteResource = "deleteResource";
    public const string DeleteMany = "deleteMany";
    public const string ReplaceFont = "replaceFont";
    public const string RepackTextures = "repackTextures";
    public const string ImportResources = "importResources";
    public const string RevertToSaved = "revertToSaved";
    public const string DebuggerFlag = "debuggerFlag";
    public const string MigrateRunOptions = "migrateRunOptions";
    public const string TimelineCopyCode = "timelineCopyCode";
    public const string TimelineCopyShare = "timelineCopyShare";

    public static readonly IReadOnlyList<PromptDef> All =
    [
        new(CloseUnsaved, false),
        new(TempRunWarning, false),
        new(RiskyAction),
        new(UnsavedChanges),
        new(CopyToSave),
        new(CopyKeepOrganize),
        new(DeleteResource),
        new(DeleteMany),
        new(ReplaceFont),
        new(RepackTextures),
        new(ImportResources),
        new(RevertToSaved),
        new(DebuggerFlag),
        new(MigrateRunOptions),
        new(TimelineCopyCode, false),
        new(TimelineCopyShare, false),
    ];

    public static string TitleKey(string id) => "prompts.item." + id;

    public static PromptChoice Get(AppSettings s, string id) => id switch
    {
        CloseUnsaved => s.AskOnClose ? PromptChoice.Ask : PromptChoice.Yes, // yes: close and keep the changes in the journal
        TempRunWarning => s.WarnTempRun ? PromptChoice.Ask : PromptChoice.Yes, // yes: run without the notice
        RiskyAction => RiskPolicy.Of(s) switch { RiskChoice.Allow => PromptChoice.Yes, RiskChoice.Refuse => PromptChoice.No, _ => PromptChoice.Ask },
        _ => s.Prompts.TryGetValue(id, out var text) && Enum.TryParse<PromptChoice>(text, true, out var choice) && IsKnown(id, choice) ? choice : PromptChoice.Ask,
    };

    /// <summary>The settings with the choice for one prompt; an id that is not in <see cref="All"/> (a security prompt) changes nothing.</summary>
    public static AppSettings Set(AppSettings s, string id, PromptChoice choice)
    {
        if (!IsKnown(id, choice)) return s;
        return id switch
        {
            CloseUnsaved => s with { AskOnClose = choice == PromptChoice.Ask },
            TempRunWarning => s with { WarnTempRun = choice == PromptChoice.Ask },
            RiskyAction => choice switch
            {
                PromptChoice.Yes => s with { AllowUnsafeSilently = true },
                PromptChoice.No => s with { AskRiskyActions = false, AllowUnsafeSilently = false },
                _ => s with { AskRiskyActions = true, AllowUnsafeSilently = false },
            },
            _ => s with { Prompts = choice == PromptChoice.Ask ? s.Prompts.Where(p => p.Key != id).ToDictionary() : new Dictionary<string, string>(s.Prompts) { [id] = choice.ToString() } },
        };
    }

    public static AppSettings ResetAll(AppSettings s) => All.Aggregate(s with { Prompts = new Dictionary<string, string>() }, (x, p) => Set(x, p.Id, PromptChoice.Ask));

    /// <summary>True for a prompt in the list and a choice it allows.</summary>
    public static bool IsKnown(string id, PromptChoice choice) => All.FirstOrDefault(p => p.Id == id) is { } def && (choice != PromptChoice.No || def.CanSayNo);
}
