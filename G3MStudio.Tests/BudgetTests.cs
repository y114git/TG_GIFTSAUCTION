namespace G3MStudio.Tests;

public sealed class BudgetTests
{
    [Fact]
    public void AMissWithinTheSlowdownOfTheMachineIsToleratedAndALargeOneStillFails()
    {
        Assert.True(Budget.Slowdown >= 1);
        Budget.AssertUnder(0.5, 1, "fast");
        if (Budget.OnSharedRunner) return;
        Assert.ThrowsAny<Exception>(() => Budget.AssertUnder(1e9, 1, "far over"));
    }
}
