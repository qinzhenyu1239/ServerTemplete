param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [ValidateRange(100, 10000000)]
    [int]$RecordCount = 100000,
    [ValidateRange(1, 512)]
    [int]$ConcurrentPlayers = 32,
    [ValidateRange(1, 100)]
    [int]$DrawsPerPlayer = 10,
    [ValidateRange(100, 2000)]
    [int]$InsertBatchSize = 500,
    [double]$MaxP95Milliseconds = 0,
    [double]$MaxRecentQueryMilliseconds = 0,
    [switch]$KeepDatabaseData,
    [switch]$AllowLargeDataSet,
    [switch]$ApplyHistoryIndex,
    [string]$DatabaseConfigurationPath = (Join-Path $PSScriptRoot "../GameServer/App.config")
)

$ErrorActionPreference = "Stop"
if ($RecordCount -gt 1000000 -and -not $AllowLargeDataSet) {
    throw "RecordCount above 1,000,000 requires -AllowLargeDataSet."
}

$testProject = Join-Path $PSScriptRoot "GameServerTests.csproj"
$historyIndexMigration = Join-Path $PSScriptRoot `
    "../sql/migrations/20260905_001_gacha_history_index.sql"
$resolvedConfigurationPath = (Resolve-Path $DatabaseConfigurationPath).Path
$reportDirectory = Join-Path $PSScriptRoot "TestResults"
$reportPath = Join-Path $reportDirectory (
    "gacha-performance-{0}.json" -f [DateTime]::UtcNow.ToString("yyyyMMdd-HHmmss"))
$basePlayerId = -[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()

$previousValues = @{
    GACHA_RUN_PERFORMANCE_TESTS = $env:GACHA_RUN_PERFORMANCE_TESTS
    GACHA_DB_CONFIGURATION_PATH = $env:GACHA_DB_CONFIGURATION_PATH
    GACHA_KEEP_DATABASE_DATA = $env:GACHA_KEEP_DATABASE_DATA
    GACHA_TEST_PLAYER_ID = $env:GACHA_TEST_PLAYER_ID
    GACHA_PERF_RECORD_COUNT = $env:GACHA_PERF_RECORD_COUNT
    GACHA_PERF_CONCURRENT_PLAYERS = $env:GACHA_PERF_CONCURRENT_PLAYERS
    GACHA_PERF_DRAWS_PER_PLAYER = $env:GACHA_PERF_DRAWS_PER_PLAYER
    GACHA_PERF_INSERT_BATCH_SIZE = $env:GACHA_PERF_INSERT_BATCH_SIZE
    GACHA_PERF_MAX_P95_MS = $env:GACHA_PERF_MAX_P95_MS
    GACHA_PERF_MAX_RECENT_QUERY_MS = $env:GACHA_PERF_MAX_RECENT_QUERY_MS
    GACHA_PERF_REPORT_PATH = $env:GACHA_PERF_REPORT_PATH
    GACHA_PERF_APPLY_HISTORY_INDEX = $env:GACHA_PERF_APPLY_HISTORY_INDEX
    GACHA_PERF_HISTORY_INDEX_MIGRATION_PATH = $env:GACHA_PERF_HISTORY_INDEX_MIGRATION_PATH
}

try {
    $env:GACHA_RUN_PERFORMANCE_TESTS = "1"
    $env:GACHA_DB_CONFIGURATION_PATH = $resolvedConfigurationPath
    $env:GACHA_KEEP_DATABASE_DATA = if ($KeepDatabaseData) { "1" } else { "0" }
    $env:GACHA_TEST_PLAYER_ID = $basePlayerId.ToString()
    $env:GACHA_PERF_RECORD_COUNT = $RecordCount.ToString()
    $env:GACHA_PERF_CONCURRENT_PLAYERS = $ConcurrentPlayers.ToString()
    $env:GACHA_PERF_DRAWS_PER_PLAYER = $DrawsPerPlayer.ToString()
    $env:GACHA_PERF_INSERT_BATCH_SIZE = $InsertBatchSize.ToString()
    $env:GACHA_PERF_MAX_P95_MS = $MaxP95Milliseconds.ToString()
    $env:GACHA_PERF_MAX_RECENT_QUERY_MS = $MaxRecentQueryMilliseconds.ToString()
    $env:GACHA_PERF_REPORT_PATH = $reportPath
    $env:GACHA_PERF_APPLY_HISTORY_INDEX = if ($ApplyHistoryIndex) { "1" } else { "0" }
    $env:GACHA_PERF_HISTORY_INDEX_MIGRATION_PATH = $historyIndexMigration

    dotnet test $testProject --configuration $Configuration --filter "Category=Performance"
    if ($LASTEXITCODE -ne 0) {
        throw "Gacha performance tests failed with exit code $LASTEXITCODE."
    }

    Write-Output "PASS: gacha capacity and load tests"
    Write-Output "REPORT: $reportPath"
    Get-Content -Raw -LiteralPath $reportPath
}
finally {
    foreach ($name in $previousValues.Keys) {
        [Environment]::SetEnvironmentVariable($name, $previousValues[$name], "Process")
    }
}
