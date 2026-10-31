namespace G3MStudio.Tests;

/// <summary>
/// Time budgets of the design documents. On a developer machine a miss fails the test. A shared CI runner is several times slower and noisier
/// than the reference machine, so there a miss is only written to the test output; the numbers are still recorded in every run.
/// </summary>
internal static class Budget
{
    public static bool OnSharedRunner => Environment.GetEnvironmentVariable("CI") is "true";

    public static void AssertUnder(double measured, double limit, string what, ITestOutputHelper? output = null)
    {
        if (measured < limit) return;
        var message = $"{what}: {measured:F1} ms, budget {limit:F0} ms";
        if (OnSharedRunner) output?.WriteLine("over budget on a shared runner: " + message);
        else Assert.Fail(message);
    }
}
