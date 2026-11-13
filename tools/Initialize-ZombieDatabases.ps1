#requires -Version 7.0
[CmdletBinding()]
param(
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot '../GameServer/App.config'),
    [string]$MySqlPath = 'C:/Program Files/MySQL/MySQL Server 5.7/bin/mysql.exe',
    [switch]$VerifyOnly
)

$ErrorActionPreference = 'Stop'
if (!(Test-Path -LiteralPath $MySqlPath)) { throw "MySQL client not found: $MySqlPath" }
[xml]$configuration = Get-Content -LiteralPath $ConfigurationPath -Raw
$connections = @{}
foreach ($name in @('connAuthUserStr', 'connGameDataStr')) {
    $entry = $configuration.configuration.connectionStrings.add | Where-Object { $_.GetAttribute('name') -eq $name }
    if ($null -eq $entry) { throw "Missing connection: $name" }
    $fields = @{}
    foreach ($field in $entry.GetAttribute('connectionString').Split(';')) {
        $pair = $field.Split('=', 2)
        if ($pair.Length -eq 2) { $fields[$pair[0].Trim()] = $pair[1].Trim() }
    }
    $connections[$name] = $fields
}
$auth = $connections['connAuthUserStr']
$game = $connections['connGameDataStr']
if ($auth['Database'] -ne 'zombie_auth' -or $game['Database'] -ne 'zombie_game') {
    throw 'This initializer only accepts zombie_auth and zombie_game. Original databases are never initialized.'
}
foreach ($key in @('Data Source', 'Port', 'User Id', 'Password')) {
    if ($auth[$key] -ne $game[$key]) { throw "Both databases must use the same MySQL connection settings: $key" }
}

function Invoke-MySql([string]$Sql) {
    $start = [System.Diagnostics.ProcessStartInfo]::new($MySqlPath)
    $start.UseShellExecute = $false
    $start.CreateNoWindow = $true
    $start.RedirectStandardInput = $true
    $start.RedirectStandardOutput = $true
    $start.RedirectStandardError = $true
    $start.StandardInputEncoding = [System.Text.UTF8Encoding]::new($false)
    $start.StandardOutputEncoding = [System.Text.UTF8Encoding]::new($false)
    foreach ($argument in @('--no-defaults', '--protocol=TCP', '--batch', '--raw', '--skip-column-names',
        '--default-character-set=utf8mb4', "--host=$($auth['Data Source'])", "--port=$($auth['Port'])", "--user=$($auth['User Id'])")) {
        $start.ArgumentList.Add($argument)
    }
    # Credentials stay in the child environment, never in command arguments or generated files.
    $start.Environment['MYSQL_PWD'] = $auth['Password']
    $process = [System.Diagnostics.Process]::Start($start)
    try {
        $output = $process.StandardOutput.ReadToEndAsync()
        $errors = $process.StandardError.ReadToEndAsync()
        $process.StandardInput.WriteLine($Sql)
        $process.StandardInput.Close()
        $process.WaitForExit()
        $errorText = $errors.GetAwaiter().GetResult()
        if ($process.ExitCode -ne 0) { throw "MySQL failed: $errorText" }
        return $output.GetAwaiter().GetResult().Trim()
    }
    finally { $process.Dispose() }
}

if (!$VerifyOnly) {
    $schemaPath = Join-Path $PSScriptRoot '../sql/zombie_init.sql'
    $schema = Get-Content -LiteralPath $schemaPath -Raw
    if ($schema -match '(?i)auth_10001|ugame_10001|DROP\s+(DATABASE|TABLE)|TRUNCATE|INSERT\s+INTO|REPLACE\s+INTO') {
        throw 'Unexpected database name, data import or destructive command in zombie_init.sql.'
    }
    Invoke-MySql $schema | Out-Null
}

$tableNames = Invoke-MySql @'
SELECT TABLE_SCHEMA, TABLE_NAME FROM information_schema.TABLES
WHERE TABLE_SCHEMA IN ('zombie_auth', 'zombie_game') AND TABLE_TYPE='BASE TABLE'
ORDER BY TABLE_SCHEMA, TABLE_NAME;
'@
if ([string]::IsNullOrWhiteSpace($tableNames)) { throw 'Zombie database tables were not found.' }
$counts = foreach ($line in ($tableNames -split '\r?\n')) {
    $parts = $line.Split("`t")
    if ($parts.Length -ne 2 -or $parts[0] -notin @('zombie_auth', 'zombie_game') -or $parts[1] -notmatch '^[a-z0-9_]+$') {
        throw 'Unexpected table identifier returned by MySQL.'
    }
    "SELECT '$($parts[0])' AS db_name, '$($parts[1])' AS table_name, COUNT(*) AS row_count FROM ``$($parts[0])``.``$($parts[1])``;"
}
Invoke-MySql ($counts -join "`n")
Write-Output 'Zombie schemas verified; no original database was modified.'
