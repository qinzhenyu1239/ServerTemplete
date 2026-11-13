-- Target: zombie_game only. Back up player before running; MySQL 5.7 DDL auto-commits.
USE `zombie_game`;
SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='level'), 'ALTER TABLE zombie_game.player DROP COLUMN `level`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='guildId'), 'ALTER TABLE zombie_game.player DROP COLUMN `guildId`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='titleId'), 'ALTER TABLE zombie_game.player DROP COLUMN `titleId`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='signature'), 'ALTER TABLE zombie_game.player DROP COLUMN `signature`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='avatarFrameId'), 'ALTER TABLE zombie_game.player DROP COLUMN `avatarFrameId`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='backgroundId'), 'ALTER TABLE zombie_game.player DROP COLUMN `backgroundId`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='displayAchievementIds'), 'ALTER TABLE zombie_game.player DROP COLUMN `displayAchievementIds`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='displayCharacterIds'), 'ALTER TABLE zombie_game.player DROP COLUMN `displayCharacterIds`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='usex'), 'ALTER TABLE zombie_game.player DROP COLUMN `usex`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='job'), 'ALTER TABLE zombie_game.player DROP COLUMN `job`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='exp'), 'ALTER TABLE zombie_game.player DROP COLUMN `exp`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='HP'), 'ALTER TABLE zombie_game.player DROP COLUMN `HP`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='MP'), 'ALTER TABLE zombie_game.player DROP COLUMN `MP`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='equipment'), 'ALTER TABLE zombie_game.player DROP COLUMN `equipment`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='ucoin'), 'ALTER TABLE zombie_game.player DROP COLUMN `ucoin`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='umoney'), 'ALTER TABLE zombie_game.player DROP COLUMN `umoney`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='lastDailyReset'), 'ALTER TABLE zombie_game.player DROP COLUMN `lastDailyReset`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='vipRightJson'), 'ALTER TABLE zombie_game.player DROP COLUMN `vipRightJson`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='platform'), 'ALTER TABLE zombie_game.player DROP COLUMN `platform`', 'SELECT 1');
PREPARE minimal_player_statement FROM @sql;
EXECUTE minimal_player_statement;
DEALLOCATE PREPARE minimal_player_statement;

