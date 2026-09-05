using Xunit;

namespace GameServerTests;

public sealed class DatabaseFactAttribute : FactAttribute
{
    public DatabaseFactAttribute()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("GACHA_RUN_DATABASE_TESTS"),
                "1",
                StringComparison.Ordinal))
        {
            Skip = "Set GACHA_RUN_DATABASE_TESTS=1 through run-tests.ps1 to run database tests.";
        }
    }
}
