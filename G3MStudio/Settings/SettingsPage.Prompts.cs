using Avalonia.Controls;
using Avalonia.Layout;
using G3MStudio.Core.ModPack;
using G3MStudio.Core.Settings;
using G3MStudio.Localization;
using G3MStudio.Mods;

namespace G3MStudio.Settings;

public sealed partial class SettingsPage
{
    private static readonly PromptChoice[] PromptChoices = [PromptChoice.Ask, PromptChoice.Yes, PromptChoice.No];

    /// <summary>Every prompt that offers "Don't ask again", with the answer it uses now; a prompt silenced by mistake is turned back to asking here.</summary>
    private Control[] PromptsSection(AppSettings s)
    {
        var reset = new Button { Name = "ResetPrompts", Content = Loc.Current["prompts.resetAll"] };
        reset.Click += (_, _) =>
        {
            _store.Update(Prompts.ResetAll);
            Content = Build();
        };
        var controls = new List<Control> { Heading("settings.prompts"), new TextBlock { Text = Loc.Current["prompts.note"], Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap }, reset };
        foreach (var def in Prompts.All)
        {
            var title = Loc.Current[Prompts.TitleKey(def.Id)];
            var current = Prompts.Get(s, def.Id);
            if (def.CanSayNo)
            {
                var choices = PromptChoices.ToList();
                var box = new ComboBox { Name = "Prompt_" + def.Id, ItemsSource = choices.Select(c => Loc.Current["prompts.choice." + c]).ToList(), SelectedIndex = choices.IndexOf(current), MinWidth = Wide(12.5) };
                box.SelectionChanged += (_, _) => { if (!_building && box.SelectedIndex >= 0) _store.Update(x => Prompts.Set(x, def.Id, choices[box.SelectedIndex])); };
                controls.Add(Row(Prompts.TitleKey(def.Id), box));
                continue;
            }

            // Two actions to pick from, or a notice: a switch for "ask every time".
            var ask = new CheckBox { Name = "Prompt_" + def.Id, Tag = title, Content = title, IsChecked = current == PromptChoice.Ask };
            ask.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => Prompts.Set(x, def.Id, ask.IsChecked == true ? PromptChoice.Ask : PromptChoice.Yes)); };
            controls.Add(ask);
        }

        return [.. controls];
    }

    /// <summary>The cached originals that Export Mod Pack compares against (Settings, mod base).</summary>
    private Control[] ModBaseSection(AppSettings s)
    {
        var cache = new ModBaseCache(_store.UserData);
        var enabled = new CheckBox { Name = "ModBaseEnabled", Tag = Loc.Current["modBase.enabled"], Content = Loc.Current["modBase.enabled"], IsChecked = s.ModBaseEnabled };
        enabled.IsCheckedChanged += (_, _) => { if (!_building) _store.Update(x => x with { ModBaseEnabled = enabled.IsChecked == true }); };
        var size = new TextBlock { Name = "ModBaseSize", Classes = { "muted" }, VerticalAlignment = VerticalAlignment.Center };
        void ShowSize() => size.Text = Loc.Current.Format("modBase.size", ("size", (cache.TotalBytes / 1048576.0).ToString("0.#", System.Globalization.CultureInfo.CurrentCulture)));
        ShowSize();
        var clear = new Button { Name = "ModBaseClear", Content = Loc.Current["modBase.clear"] };
        clear.Click += (_, _) =>
        {
            _ = cache.Clear(); // a locked cache keeps its size on screen
            ShowSize();
        };
        var cap = Number("ModBaseMaxMb", 64, 1_048_576, 256, s.ModBaseMaxMb, (x, n) =>
        {
            cache.Trim(n * 1024L * 1024L); // a lower cap frees space at once
            return x with { ModBaseMaxMb = n };
        });
        return
        [
            Heading("settings.modBase"),
            new TextBlock { Text = Loc.Current["modBase.note"], Classes = { "muted" }, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
            enabled,
            Row("modBase.cap", cap, new TextBlock { Text = "MB", VerticalAlignment = VerticalAlignment.Center }),
            new StackPanel { Orientation = Orientation.Horizontal, Spacing = ModsUi.Space(2), Tag = Loc.Current["modBase.clear"], Children = { size, clear } },
        ];
    }
}
