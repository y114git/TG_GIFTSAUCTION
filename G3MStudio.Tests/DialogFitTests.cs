using G3MStudio.Theme;

namespace G3MStudio.Tests;

/// <summary>A dialog that grew after it opened must not keep its buttons under the taskbar.</summary>
public sealed class DialogFitTests
{
    [Theory]
    [InlineData(365, 831, 0, 1152, 321)] // the recorder after a take: 365 + 831 passes the work area, so it moves up
    [InlineData(100, 500, 0, 1152, 100)] // fits: stays
    [InlineData(300, 1300, 24, 1152, 24)] // taller than the area: its top meets the area top
    public void TheTopMovesUpOnlyWhenTheBottomLeavesTheWorkArea(int top, int height, int areaTop, int areaBottom, int expected) =>
        Assert.Equal(expected, DialogSizing.FitTop(top, height, areaTop, areaBottom));
}
