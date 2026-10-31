using System.Globalization;
using G3MStudio.Core.Localization;
using G3MStudio.Localization;

namespace G3MStudio;

/// <summary>The first-launch welcome window and its command in the Help menu.</summary>
public sealed partial class MainWindow
{
    public void ShowOnboarding()
    {
        var settings = Workspaces.Settings;
        var languages = Loc.Current.Languages.OrderBy(l => l.Key, StringComparer.Ordinal).ToList();
        var detected = Localizer.Detect(CultureInfo.CurrentUICulture, languages.Select(l => l.Key));
        new OnboardingWindow(languages, settings.Current.Language, detected, key => settings.Update(x => x with { Language = key }), () => settings.Update(x => x with { OnboardingDone = true })).Show(this);
    }

    /// <summary>Shows the welcome window once, on the first launch, with the language of the operating system chosen. G3MSTUDIO_NO_ONBOARDING=1 skips it (automated runs).</summary>
    public void RunFirstLaunch()
    {
        var settings = Workspaces.Settings;
        if (settings.Current.OnboardingDone || Environment.GetEnvironmentVariable("G3MSTUDIO_NO_ONBOARDING") == "1") return;
        var detected = Localizer.Detect(CultureInfo.CurrentUICulture, Loc.Current.Languages.Keys);
        if (settings.Current.Language != detected) settings.Update(x => x with { Language = detected });
        ShowOnboarding();
    }
}
