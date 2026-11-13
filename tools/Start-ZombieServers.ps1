#requires -Version 7.0
[CmdletBinding()]
param([switch]$PrepareOnly, [switch]$SkipBuild, [switch]$Stop)

$ErrorActionPreference = 'Stop'
$serverRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runtimeRoot = Join-Path $serverRoot 'GameServer/bin/ZombieLocal'
$buildRoot = Join-Path $runtimeRoot 'Build'
$roles = @('ServerZone', 'Login', 'Game')
$dotnetProcesses = @(Get-CimInstance Win32_Process -Filter "name = 'dotnet.exe'")

# Each process reads its own configuration. Never reuse another project's deployment.
foreach ($role in $roles) {
    $roleDirectory = Join-Path $runtimeRoot $role
    $running = $dotnetProcesses | Where-Object {
        $_.CommandLine -and $_.CommandLine.Contains((Join-Path $roleDirectory 'GameServer.dll'))
    }
    if ($Stop) {
        $running | ForEach-Object { Stop-Process -Id $_.ProcessId -ErrorAction SilentlyContinue }
        continue
    }
    if ($running) { throw "Zombie $role is already running. Stop that process before preparing its files." }
}
if ($Stop) { Write-Output 'Stopped only the local zombie server roles.'; return }
if (!$SkipBuild) {
    & dotnet build (Join-Path $serverRoot 'GameServer/GameServer.csproj') --no-restore --verbosity quiet -o $buildRoot
    if ($LASTEXITCODE -ne 0) { throw 'Zombie server build failed.' }
}
if (!(Test-Path -LiteralPath (Join-Path $buildRoot 'GameServer.dll'))) { throw 'Build the zombie server first.' }

[xml]$sourceConfig = Get-Content -LiteralPath (Join-Path $serverRoot 'GameServer/App.config') -Raw
foreach ($expected in @{ connAuthUserStr = 'zombie_auth'; connGameDataStr = 'zombie_game' }.GetEnumerator()) {
    $entry = $sourceConfig.configuration.connectionStrings.add | Where-Object { $_.GetAttribute('name') -eq $expected.Key }
    if ($null -eq $entry -or $entry.GetAttribute('connectionString') -notmatch "(?i)(^|;)Database=$($expected.Value)(;|$)") {
        throw "Expected isolated database $($expected.Value) in App.config."
    }
}
foreach ($role in $roles) {
    $roleDirectory = Join-Path $runtimeRoot $role
    New-Item -ItemType Directory -Path $roleDirectory -Force | Out-Null
    Get-ChildItem -LiteralPath $buildRoot | Copy-Item -Destination $roleDirectory -Recurse -Force
    $roleConfig = $sourceConfig.Clone()
    ($roleConfig.configuration.appSettings.add | Where-Object { $_.GetAttribute('key') -eq 'ServerRole' }).SetAttribute('value', $role)
    $roleConfig.Save((Join-Path $roleDirectory 'GameServer.dll.config'))
}
if ($PrepareOnly) { Write-Output "Zombie role configurations prepared: $runtimeRoot"; return }

foreach ($port in @(16090, 16091, 7080, 7082, 6090)) {
    $listener = Get-NetTCPConnection -State Listen -LocalPort $port -ErrorAction SilentlyContinue
    if ($listener) { throw "Port $port is occupied (PID $($listener.OwningProcess -join ',')). No existing process was stopped." }
}
$processes = @()
try {
    foreach ($role in $roles) {
        $roleDirectory = Join-Path $runtimeRoot $role
        $start = [Diagnostics.ProcessStartInfo]::new((Get-Command dotnet).Source)
        $start.ArgumentList.Add((Join-Path $roleDirectory 'GameServer.dll'))
        $start.WorkingDirectory = $roleDirectory
        $start.UseShellExecute = $false
        $start.CreateNoWindow = $true
        # ServerStartup waits on Console.ReadLine; keep stdin open for the server lifetime.
        $start.RedirectStandardInput = $true
        # Use this deployment's signing key even if the launching shell has an older override.
        $start.Environment.Remove('GAME_TICKET_HMAC_SECRET') | Out-Null
        $process = [Diagnostics.Process]::Start($start)
        $processes += $process
        Write-Output "Zombie $role started (PID $($process.Id))."
    }
    $processes | ForEach-Object { [pscustomobject]@{ Id = $_.Id; Executable = $_.StartInfo.FileName; Assembly = $_.StartInfo.ArgumentList[0] } } |
        ConvertTo-Json | Set-Content -LiteralPath (Join-Path $runtimeRoot 'processes.json')
    while ($true) {
        foreach ($process in $processes) {
            if ($process.WaitForExit(300)) { throw "Zombie server PID $($process.Id) exited with code $($process.ExitCode). Check its Logs directory." }
        }
    }
}
finally {
    foreach ($process in $processes) {
        if (!$process.HasExited) {
            $process.StandardInput.WriteLine()
            $process.StandardInput.Close()
            if (!$process.WaitForExit(10000)) { $process.Kill() }
        }
        $process.Dispose()
    }
}
