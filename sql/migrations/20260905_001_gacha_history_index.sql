-- MySQL 5.7 compatible and safe to run repeatedly on the selected game database.
SET @gacha_history_index_exists = (
    SELECT COUNT(*)
    FROM information_schema.statistics
    WHERE table_schema = DATABASE()
      AND table_name = 'player_gacha_record'
      AND index_name = 'idx_gacha_player_created_id'
);

SET @gacha_history_index_sql = IF(
    @gacha_history_index_exists = 0,
    'ALTER TABLE player_gacha_record ADD INDEX idx_gacha_player_created_id (player_id, created_at, id), ALGORITHM=INPLACE, LOCK=NONE',
    'SELECT 1'
);

PREPARE gacha_history_index_statement FROM @gacha_history_index_sql;
EXECUTE gacha_history_index_statement;
DEALLOCATE PREPARE gacha_history_index_statement;
