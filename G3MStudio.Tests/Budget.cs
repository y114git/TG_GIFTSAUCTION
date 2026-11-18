namespace G3MStudio.Tests;

/// <summary>
/// Time budgets of the design documents. On a developer machine a miss fails the test. A shared CI runner is several times slower and noisier
/// than the reference machine, so there a miss is only written to the test output; the numbers are still recorded in every run.
/// </summary>
internal static class Budget
{
    /// <summary>The time of <see cref="Calibrate"/> on an idle developer machine. A busy machine (several test processes, a build) takes longer for the same work, and the budgets stretch by that factor.</summary>
    private const double ReferenceMs = 36;

    private static double Calibrate()
    {
        var best = double.MaxValue;
        for (var run = 0; run < 7; run++)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var x = 2463534242u;
            for (var i = 0; i < 20_000_000; i++)
            {
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
            }

            GC.KeepAlive(x); // the loop must not be dropped as unused
            best = Math.Min(best, watch.Elapsed.TotalMilliseconds);
        }

        return best;
    }

    /// <summary>How many times slower than an idle machine this one is right now; never below 1, so a quiet machine keeps the exact budget.</summary>
    public static double Slowdown => Math.Max(1, Calibrate() / ReferenceMs);

    public static bool OnSharedRunner => Environment.GetEnvironmentVariable("CI") is "true";

    public static void AssertUnder(double measured, double limit, string what, ITestOutputHelper? output = null)
    {
        if (measured < limit) return;
        var slowdown = Slowdown;
        if (measured < limit * slowdown) return;
        var message = $"{what}: {measured:F1} ms, budget {limit:F0} ms (machine {slowdown:F1} times slower than idle)";
        if (OnSharedRunner) output?.WriteLine("over budget on a shared runner: " + message);
        else Assert.Fail(message);
    }
}
