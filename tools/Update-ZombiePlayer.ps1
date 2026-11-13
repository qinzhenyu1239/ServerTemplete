#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$BackupDirectory = (Join-Path $env:LOCALAPPDATA ('Temp/zombie-player-backup-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))),
    [string]$MySqlDirectory = 'C:/Program Files/MySQL/MySQL Server 5.7/bin'
)
$ErrorActionPreference = 'Stop'
$serverRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
[xml]$config = Get-Content -LiteralPath (Join-Path $serverRoot 'GameServer/App.config') -Raw
$connection = ($config.configuration.connectionStrings.add | Where-Object { $_.name -eq 'connGameDataStr' }).connectionString
$settings = @{}
foreach ($part in $connection.Split(';')) { $pair = $part.Split('=', 2); if ($pair.Count -eq 2) { $settings[$pair[0].Trim()] = $pair[1].Trim() } }
if ($settings.Database -ne 'zombie_game') { throw 'Only zombie_game can be migrated.' }
function Invoke-Database([string]$Executable, [string[]]$ExtraArguments, [string]$InputSql) {
    $start = [Diagnostics.ProcessStartInfo]::new((Join-Path $MySqlDirectory $Executable))
    $start.UseShellExecute = $false; $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true; $start.RedirectStandardOutput = $true; $start.RedirectStandardError = $true
    $start.StandardInputEncoding = [Text.UTF8Encoding]::new($false)
    $start.StandardOutputEncoding = [Text.UTF8Encoding]::new($false)
    foreach ($arg in @('--no-defaults', '--protocol=TCP', '--default-character-set=utf8mb4', "--host=$($settings['Data Source'])", "--port=$($settings.Port)", "--user=$($settings['User Id'])") + $ExtraArguments) { $start.ArgumentList.Add($arg) }
    $start.Environment['MYSQL_PWD'] = $settings.Password
    $process = [Diagnostics.Process]::Start($start)
    try {
        $output = $process.StandardOutput.ReadToEndAsync(); $errors = $process.StandardError.ReadToEndAsync()
        if ($InputSql) { $process.StandardInput.WriteLine($InputSql) }; $process.StandardInput.Close(); $process.WaitForExit()
        $errorText = $errors.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "Database command failed: $errorText" }
        return $output.GetAwaiter().GetResult()
    } finally { $process.Dispose() }
}
# Only stop executable paths within this project's GameServer/bin directory.
$binRoot = [IO.Path]::GetFullPath((Join-Path $serverRoot 'GameServer/bin')) + [IO.Path]::DirectorySeparatorChar
Get-CimInstance Win32_Process | Where-Object {
    ($_.Name -eq 'GameServer.exe' -and $_.ExecutablePath -and $_.ExecutablePath.StartsWith($binRoot, [StringComparison]::OrdinalIgnoreCase)) -or
    ($_.Name -eq 'dotnet.exe' -and $_.CommandLine -and $_.CommandLine.Contains($binRoot) -and $_.CommandLine.Contains('GameServer.dll'))
} | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction Stop }
New-Item -ItemType Directory -Path $BackupDirectory -Force | Out-Null
$backup = Join-Path $BackupDirectory 'zombie_game.player.sql'
$dump = Invoke-Database 'mysqldump.exe' @('--single-transaction', '--skip-lock-tables', '--set-gtid-purged=OFF', 'zombie_game', 'player') ''
if ($dump -notmatch 'CREATE TABLE.*player') { throw 'Player backup was not produced.' }
[IO.File]::WriteAllText($backup, $dump, [Text.UTF8Encoding]::new($false))
$queryArguments = @('--batch', '--raw', '--skip-column-names', 'zombie_game')
$readKept = 'SELECT id,accountId,HEX(name),avatarId,createTime,status FROM player ORDER BY id;'
$before = Invoke-Database 'mysql.exe' $queryArguments $readKept
$migration = (Get-Content -LiteralPath (Join-Path $serverRoot 'sql/migrations/20261112_001_zombie_player_minimal.sql') -Raw) + "`n" +
    (Get-Content -LiteralPath (Join-Path $serverRoot 'sql/migrations/20261112_002_zombie_player_progress.sql') -Raw)
Invoke-Database 'mysql.exe' $queryArguments $migration | Out-Null
Invoke-Database 'mysql.exe' $queryArguments $migration | Out-Null
$after = Invoke-Database 'mysql.exe' $queryArguments $readKept
if ($before -cne $after) { throw "Retained player data changed. Backup: $backup" }
$columns = Invoke-Database 'mysql.exe' $queryArguments "SELECT COLUMN_NAME FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' ORDER BY ORDINAL_POSITION;"
if (@($columns.Trim() -split '\r?\n' | Sort-Object) -join ',' -ne 'accountId,avatarId,baseLevel,createTime,id,mainStage,name,resonanceLevel,status,towerStage') { throw 'Unexpected player columns.' }
Write-Output "Player migration verified twice; retained data matches. Backup: $backup"
