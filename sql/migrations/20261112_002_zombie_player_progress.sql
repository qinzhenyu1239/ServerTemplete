-- Target zombie_game only; additive migration compatible with MySQL 5.7.
USE `zombie_game`;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='baseLevel'), 'SELECT 1', 'ALTER TABLE zombie_game.player ADD COLUMN `baseLevel` int(11) NOT NULL DEFAULT 1 COMMENT ''基地等级''');
PREPARE player_progress_statement FROM @sql;
EXECUTE player_progress_statement;
DEALLOCATE PREPARE player_progress_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='mainStage'), 'SELECT 1', 'ALTER TABLE zombie_game.player ADD COLUMN `mainStage` int(11) NOT NULL DEFAULT 1 COMMENT ''主线关卡章节编号''');
PREPARE player_progress_statement FROM @sql;
EXECUTE player_progress_statement;
DEALLOCATE PREPARE player_progress_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='resonanceLevel'), 'SELECT 1', 'ALTER TABLE zombie_game.player ADD COLUMN `resonanceLevel` int(11) NOT NULL DEFAULT 0 COMMENT ''共鸣等级''');
PREPARE player_progress_statement FROM @sql;
EXECUTE player_progress_statement;
DEALLOCATE PREPARE player_progress_statement;

SET @sql = IF(EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='zombie_game' AND table_name='player' AND column_name='towerStage'), 'SELECT 1', 'ALTER TABLE zombie_game.player ADD COLUMN `towerStage` int(11) NOT NULL DEFAULT 0 COMMENT ''爬塔关卡''');
PREPARE player_progress_statement FROM @sql;
EXECUTE player_progress_statement;
DEALLOCATE PREPARE player_progress_statement;
