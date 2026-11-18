using Avalonia.Controls;
using Avalonia.VisualTree;
using G3MStudio.Localization;

namespace G3MStudio.Tests;

/// <summary>A dialog that saves settings while it closes must not make the other settings listeners touch the closed window.</summary>
public sealed class DialogSettingsFeedbackTests
{
    [Theory]
    [InlineData("OnboardingSkip")]
    [InlineData("OnboardingStart")]
    public async Task ClosingTheWelcomeWindowWhileSettingsChangeDoesNotApplyToTheClosedWindow(string button)
    {
        using var dir = new TempDir();
        await HeadlessApp.Run(() =>
        {
            var window = TabsWorkspaceTests.NewWindow(dir);
            var measured = new List<bool>();
            try
            {
                Assert.False(window.Workspaces.Settings.Current.OnboardingDone);
                window.ShowOnboarding();
                var welcome = window.OwnedWindows.OfType<OnboardingWindow>().Single();
                G3MStudio.Theme.DialogSizing.ScreenProbe = w => measured.Add(w.IsVisible);
                welcome.GetVisualDescendants().OfType<Button>().Single(b => b.Name == button).RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                Assert.True(window.Workspaces.Settings.Current.OnboardingDone);
                Assert.DoesNotContain(false, measured); // a window that is closed is not measured against its screen
            }
            finally
            {
                G3MStudio.Theme.DialogSizing.ScreenProbe = null;
                window.Close();
                Loc.Current.SetLanguage("en");
            }

            return Task.CompletedTask;
        });
    }
}
