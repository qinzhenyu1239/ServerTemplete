param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$IncludeDatabase,
    [switch]$KeepDatabaseData,
    [string]$DatabaseConfigurationPath
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$testProject = Join-Path $PSScriptRoot "GameServerTests.csproj"
$serverProject = Join-Path $repoRoot "GameServer/GameServer.csproj"
$databaseScript = Join-Path $PSScriptRoot "GachaMySql57Integration.ps1"

dotnet test $testProject --configuration $Configuration `
    --filter "Category!=Database&Category!=Performance"
if ($LASTEXITCODE -ne 0) {
    throw "GameServer unit tests failed with exit code $LASTEXITCODE."
}

if (-not $IncludeDatabase) {
    if ($KeepDatabaseData) {
        throw "-KeepDatabaseData requires -IncludeDatabase."
    }
    Write-Output "PASS: GameServer unit, gacha, character and configuration tests"
    Write-Output "Database tests were skipped. Use -IncludeDatabase to run them."
    exit 0
}

dotnet build $serverProject --configuration $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    throw "GameServer build failed with exit code $LASTEXITCODE."
}

$databaseArguments = @{}
if (-not [string]::IsNullOrWhiteSpace($DatabaseConfigurationPath)) {
    $databaseArguments.ConfigurationPath = $DatabaseConfigurationPath
}
if ($KeepDatabaseData) {
    $databaseArguments.KeepData = $true
}
& $databaseScript @databaseArguments
if ($LASTEXITCODE -ne 0) {
    throw "GameServer database integration tests failed with exit code $LASTEXITCODE."
}

Write-Output "PASS: all GameServer tests, including gacha and character MySQL integration"
