using Xunit;

namespace GameServerTests;

public sealed class PerformanceFactAttribute : FactAttribute
{
    public PerformanceFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("GACHA_RUN_PERFORMANCE_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Use run-performance-tests.ps1 to run performance tests.";
        }
    }
}
