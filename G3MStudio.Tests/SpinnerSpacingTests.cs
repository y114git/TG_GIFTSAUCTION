using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace G3MStudio.Tests;

/// <summary>The spacing rule for stack panels in Styles.axaml reaches into control templates; a template that lays out its parts in a stack panel must not get gaps from it.</summary>
public sealed class SpinnerSpacingTests
{
    private static async Task<Window> Shown(Control content)
    {
        var window = new Window { Content = content, Width = 400, Height = 300 };
        window.Show();
        for (var i = 0; i < 3; i++)
        {
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(10);
        }

        return window;
    }

    private static Rect In(Control control, Visual root) => new(control.TranslatePoint(default, root)!.Value, control.Bounds.Size);

    [Fact]
    public Task ANumberBoxHasNoGapBetweenItsTextAndItsTwoButtonsOrBetweenTheButtons() => HeadlessApp.Run(async () =>
    {
        var box = new NumericUpDown { Value = 3, Minimum = 1, Maximum = 9, Width = 160, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        var window = await Shown(box);
        try
        {
            var text = box.GetVisualDescendants().OfType<TextBox>().Single();
            var presenter = text.GetVisualDescendants().OfType<TextPresenter>().Single();
            var up = In(box.GetVisualDescendants().OfType<RepeatButton>().Single(b => b.Name == "PART_IncreaseButton"), window);
            var down = In(box.GetVisualDescendants().OfType<RepeatButton>().Single(b => b.Name == "PART_DecreaseButton"), window);
            var field = In(presenter, window);
            Assert.True(Math.Abs(down.Left - up.Right) < 0.5, $"gap of {down.Left - up.Right} px between the up and the down button");
            // Between the typed text and the first button there is the text box's own right padding and border, nothing more.
            var allowed = text.Padding.Right + text.BorderThickness.Right + 1;
            Assert.True(up.Left - field.Right <= allowed, $"gap of {up.Left - field.Right} px between the text and the up button, at most {allowed}");
            Assert.True(Math.Abs(up.Top - down.Top) < 0.5 && Math.Abs(up.Height - down.Height) < 0.5);
        }
        finally { window.Close(); }
    });

    [Fact]
    public Task NoTemplatePartOfTheCompositeControlsIsSpacedOutByTheStackPanelRule() => HeadlessApp.Run(async () =>
    {
        Control[] controls = [new NumericUpDown(), new DatePicker(), new TimePicker(), new CalendarDatePicker(), new SplitButton { Content = "x" }, new DropDownButton { Content = "x" }, new AutoCompleteBox(), new ComboBox(), new Expander { Header = "h" }, new ToggleSwitch(), new Slider()];
        var problems = new List<string>();
        foreach (var control in controls)
        {
            var window = await Shown(control);
            try
            {
                problems.AddRange(control.GetVisualDescendants().OfType<StackPanel>().Where(p => p.Spacing != 0 && p.Name?.StartsWith("PART_", StringComparison.Ordinal) == true).Select(p => $"{control.GetType().Name}: {p.Name} has spacing {p.Spacing}"));
            }
            finally { window.Close(); }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    });
}
