param(
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot "../GameServer/App.config"),
    [switch]$KeepData
)

$ErrorActionPreference = "Stop"
$testProject = Join-Path $PSScriptRoot "GameServerTests.csproj"
$resolvedConfigurationPath = Resolve-Path $ConfigurationPath
$previousRunDatabaseTests = $env:GACHA_RUN_DATABASE_TESTS
$previousConfigurationPath = $env:GACHA_DB_CONFIGURATION_PATH
$previousKeepDatabaseData = $env:GACHA_KEEP_DATABASE_DATA
$previousTestPlayerId = $env:GACHA_TEST_PLAYER_ID
$testPlayerId = -[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
try {
    $env:GACHA_RUN_DATABASE_TESTS = "1"
    $env:GACHA_DB_CONFIGURATION_PATH = $resolvedConfigurationPath.Path
    $env:GACHA_KEEP_DATABASE_DATA = if ($KeepData) { "1" } else { "0" }
    $env:GACHA_TEST_PLAYER_ID = $testPlayerId.ToString()
    dotnet test $testProject --filter "Category=Database"
    if ($LASTEXITCODE -ne 0) {
        throw "Gacha database integration tests failed with exit code $LASTEXITCODE."
    }
    if ($KeepData) {
        $minimumPlayerId = $testPlayerId - 199
        Write-Output "KEPT: gacha and character test data for player_id range $minimumPlayerId through $testPlayerId"
        Write-Output "Query: SELECT * FROM player_item WHERE player_id BETWEEN $minimumPlayerId AND $testPlayerId;"
        Write-Output "Query: SELECT * FROM player_character WHERE player_id BETWEEN $minimumPlayerId AND $testPlayerId;"
        Write-Output "Query: SELECT * FROM player_gacha_state WHERE player_id BETWEEN $minimumPlayerId AND $testPlayerId;"
        Write-Output "Query: SELECT * FROM player_gacha_record WHERE player_id BETWEEN $minimumPlayerId AND $testPlayerId;"
    }
}
finally {
    $env:GACHA_RUN_DATABASE_TESTS = $previousRunDatabaseTests
    $env:GACHA_DB_CONFIGURATION_PATH = $previousConfigurationPath
    $env:GACHA_KEEP_DATABASE_DATA = $previousKeepDatabaseData
    $env:GACHA_TEST_PLAYER_ID = $previousTestPlayerId
}
