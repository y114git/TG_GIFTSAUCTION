using System.Globalization;
using G3MStudio.Core.Localization;
using G3MStudio.Core.Settings;
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
        ShowTool("welcome", () => new OnboardingWindow(languages, settings.Current.Language, detected, settings.Current.CrashReports, settings.Current.UsageStats, new OnboardingActions(
            key => settings.Update(x => x with { Language = key }),
            hex => settings.Update(x => x with { Colors = new Dictionary<string, string>(x.Colors) { [ThemeColors.Accent] = hex } }),
            on => settings.Update(x => x with { CrashReports = on }),
            on => settings.Update(x => x with { UsageStats = on }),
            () =>
            {
                // the answers of the welcome window are the first facts of the usage summary; the update below starts the reporting
                settings.Update(x => x with { OnboardingDone = true });
                Telemetry.Note("onboarding.language", settings.Current.Language);
                Telemetry.Note("onboarding.accent", settings.Current.Colors.ContainsKey(ThemeColors.Accent) ? "changed" : "default");
                Telemetry.Note("onboarding.crash", settings.Current.CrashReports ? "on" : "off");
                Telemetry.Note("onboarding.usage", settings.Current.UsageStats ? "on" : "off");
            },
            Core.Links.FileAssociations.Supported ? Associate : null, Core.Links.FileAssociations.Registered(StudioPlaces).Count > 0, settings.Current.Colors.GetValueOrDefault(ThemeColors.Accent))));
    }

    private Core.Links.Places StudioPlaces => Core.Links.Places.Real(Workspaces.UserData);

    // The Getting Started box: the data files open in G3MStudio and show its icon, or are given back. The answer is a note, since the box has no room for an error.
    private void Associate(bool on)
    {
        var error = on ? Core.Links.FileAssociations.TryRegister(Environment.ProcessPath ?? "", StudioPlaces) : Core.Links.FileAssociations.TryRemove(StudioPlaces);
        if (error is not null) Notify(Core.Notifications.NotificationLevel.Warning, Loc.Current["settings.linksFailed"] + ": " + error);
    }

    public void ShowTour() => ShowTool("tour", () => new TourWindow(Docking.ShowPanel));

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
