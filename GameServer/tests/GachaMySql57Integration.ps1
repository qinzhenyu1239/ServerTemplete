param(
    [string]$ConfigurationPath = (Join-Path $PSScriptRoot "../App.config"),
    [string]$MySqlConnectorPath = (Join-Path $PSScriptRoot "../bin/Debug/net8.0/MySqlConnector.dll")
)

$ErrorActionPreference = "Stop"
Add-Type -Path (Resolve-Path $MySqlConnectorPath)

[xml]$configuration = Get-Content (Resolve-Path $ConfigurationPath)
$connectionString = ($configuration.configuration.connectionStrings.add |
    Where-Object name -eq "connGameDataStr").connectionString
if ([string]::IsNullOrWhiteSpace($connectionString)) {
    throw "connGameDataStr is missing from App.config."
}

$playerId = -[DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
$characterId = 10001
$poolId = 1

function Open-DatabaseConnection {
    $connection = [MySqlConnector.MySqlConnection]::new($connectionString)
    $connection.Open()
    return $connection
}

function Execute-NonQuery($connection, $transaction, [string]$sql) {
    $command = $connection.CreateCommand()
    $command.Transaction = $transaction
    $command.CommandText = $sql
    try { return $command.ExecuteNonQuery() }
    finally { $command.Dispose() }
}

function Read-Scalar($connection, [string]$sql) {
    $command = $connection.CreateCommand()
    $command.CommandText = $sql
    try { return [long]$command.ExecuteScalar() }
    finally { $command.Dispose() }
}

$setupConnection = Open-DatabaseConnection
try {
    # Rollback covers payment, character, fragment, pity and record writes together.
    $transaction = $setupConnection.BeginTransaction()
    Execute-NonQuery $setupConnection $transaction "INSERT INTO player_item(player_id,item_id,quantity) VALUES ($playerId,90001,10),($playerId,91001,0)"
    Execute-NonQuery $setupConnection $transaction "INSERT INTO player_gacha_state(player_id,pool_id) VALUES ($playerId,$poolId)"
    Execute-NonQuery $setupConnection $transaction "UPDATE player_item SET quantity=quantity-1 WHERE player_id=$playerId AND item_id=90001 AND quantity>=1"
    Execute-NonQuery $setupConnection $transaction "INSERT INTO player_character(player_id,character_id,star) VALUES ($playerId,$characterId,0)"
    Execute-NonQuery $setupConnection $transaction "UPDATE player_item SET quantity=quantity+1 WHERE player_id=$playerId AND item_id=91001"
    Execute-NonQuery $setupConnection $transaction "UPDATE player_gacha_state SET small_pity_count=1,big_pity_count=1,total_draw_count=1 WHERE player_id=$playerId AND pool_id=$poolId"
    Execute-NonQuery $setupConnection $transaction "INSERT INTO player_gacha_record(id,batch_id,draw_index,player_id,pool_id,character_id,rarity,is_new_character) VALUES (ABS($playerId),ABS($playerId),1,$playerId,$poolId,$characterId,1,1)"
    $transaction.Rollback()
    $transaction.Dispose()

    foreach ($table in @("player_item", "player_character", "player_gacha_state", "player_gacha_record")) {
        if ((Read-Scalar $setupConnection "SELECT COUNT(*) FROM $table WHERE player_id=$playerId") -ne 0) {
            throw "Rollback verification failed for $table."
        }
    }

    # Two transactions race for one ticket. The row lock plus quantity predicate permits one debit only.
    Execute-NonQuery $setupConnection $null "INSERT INTO player_item(player_id,item_id,quantity) VALUES ($playerId,90001,1)"
    $firstConnection = Open-DatabaseConnection
    $secondConnection = Open-DatabaseConnection
    try {
        $firstTransaction = $firstConnection.BeginTransaction()
        $secondTransaction = $secondConnection.BeginTransaction()
        $firstAffected = Execute-NonQuery $firstConnection $firstTransaction "UPDATE player_item SET quantity=quantity-1 WHERE player_id=$playerId AND item_id=90001 AND quantity>=1"

        $secondCommand = $secondConnection.CreateCommand()
        $secondCommand.Transaction = $secondTransaction
        $secondCommand.CommandText = "UPDATE player_item SET quantity=quantity-1 WHERE player_id=$playerId AND item_id=90001 AND quantity>=1"
        $secondTask = $secondCommand.ExecuteNonQueryAsync()
        Start-Sleep -Milliseconds 100
        $firstTransaction.Commit()
        $secondAffected = $secondTask.GetAwaiter().GetResult()
        $secondTransaction.Commit()

        if (($firstAffected + $secondAffected) -ne 1) { throw "Concurrent debit affected more than one row." }
        if ((Read-Scalar $setupConnection "SELECT quantity FROM player_item WHERE player_id=$playerId AND item_id=90001") -ne 0) {
            throw "Concurrent debit produced an invalid balance."
        }
    }
    finally {
        if ($secondCommand) { $secondCommand.Dispose() }
        if ($firstTransaction) { $firstTransaction.Dispose() }
        if ($secondTransaction) { $secondTransaction.Dispose() }
        if ($firstConnection) { $firstConnection.Dispose() }
        if ($secondConnection) { $secondConnection.Dispose() }
    }

    Write-Output "PASS: rollback atomicity and concurrent conditional debit"
}
finally {
    try {
        Execute-NonQuery $setupConnection $null "DELETE FROM player_gacha_record WHERE player_id=$playerId"
        Execute-NonQuery $setupConnection $null "DELETE FROM player_character WHERE player_id=$playerId"
        Execute-NonQuery $setupConnection $null "DELETE FROM player_gacha_state WHERE player_id=$playerId"
        Execute-NonQuery $setupConnection $null "DELETE FROM player_item WHERE player_id=$playerId"
    }
    finally { $setupConnection.Dispose() }
}
