-- Select the intended ugame database before running. Compatible with MySQL 5.7; rerunnable.
SET @mail_ddl=IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage' AND COLUMN_NAME='mailType'),'SELECT 1','ALTER TABLE mailmessage ADD COLUMN `mailType` tinyint NOT NULL DEFAULT 0 COMMENT ''0普通，1资源''');
PREPARE mail_stmt FROM @mail_ddl;
EXECUTE mail_stmt;
DEALLOCATE PREPARE mail_stmt;
SET @mail_ddl=IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage' AND COLUMN_NAME='title'),'SELECT 1','ALTER TABLE mailmessage ADD COLUMN `title` varchar(128) NOT NULL DEFAULT '''' COMMENT ''发送时标题快照''');
PREPARE mail_stmt FROM @mail_ddl;
EXECUTE mail_stmt;
DEALLOCATE PREPARE mail_stmt;
SET @mail_ddl=IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage' AND COLUMN_NAME='sourceType'),'SELECT 1','ALTER TABLE mailmessage ADD COLUMN `sourceType` int NOT NULL DEFAULT 0 COMMENT ''来源类型''');
PREPARE mail_stmt FROM @mail_ddl;
EXECUTE mail_stmt;
DEALLOCATE PREPARE mail_stmt;
SET @mail_ddl=IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage' AND COLUMN_NAME='sourceKey'),'SELECT 1','ALTER TABLE mailmessage ADD COLUMN `sourceKey` varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL COMMENT ''来源去重标识''');
PREPARE mail_stmt FROM @mail_ddl;
EXECUTE mail_stmt;
DEALLOCATE PREPARE mail_stmt;
SET @mail_ddl=IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage' AND COLUMN_NAME='expireTime'),'SELECT 1','ALTER TABLE mailmessage ADD COLUMN `expireTime` bigint NOT NULL DEFAULT 0 COMMENT ''到期Unix秒，0仅兼容旧邮件''');
PREPARE mail_stmt FROM @mail_ddl;
EXECUTE mail_stmt;
DEALLOCATE PREPARE mail_stmt;
SET @mail_ddl=IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage' AND COLUMN_NAME='claimTime'),'SELECT 1','ALTER TABLE mailmessage ADD COLUMN `claimTime` bigint NOT NULL DEFAULT 0 COMMENT ''附件领取Unix秒''');
PREPARE mail_stmt FROM @mail_ddl;
EXECUTE mail_stmt;
DEALLOCATE PREPARE mail_stmt;
SET @mail_ddl=IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage' AND COLUMN_NAME='deleteTime'),'SELECT 1','ALTER TABLE mailmessage ADD COLUMN `deleteTime` bigint NOT NULL DEFAULT 0 COMMENT ''删除Unix秒''');
PREPARE mail_stmt FROM @mail_ddl;
EXECUTE mail_stmt;
DEALLOCATE PREPARE mail_stmt;
SET @mail_ddl=IF(EXISTS(SELECT 1 FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage' AND COLUMN_NAME='deleteReason'),'SELECT 1','ALTER TABLE mailmessage ADD COLUMN `deleteReason` tinyint NOT NULL DEFAULT 0 COMMENT ''1玩家删除，2到期，3超容量''');
PREPARE mail_stmt FROM @mail_ddl;
EXECUTE mail_stmt;
DEALLOCATE PREPARE mail_stmt;
ALTER TABLE mailmessage MODIFY sendTime BIGINT NOT NULL DEFAULT 0,
  MODIFY readTime BIGINT NOT NULL DEFAULT 0,
  MODIFY msgBody TEXT NOT NULL,
  CONVERT TO CHARACTER SET utf8mb4;
-- ALTER CONVERT may adopt a default collation; keep source identifiers case-sensitive.
ALTER TABLE mailmessage MODIFY sourceKey VARCHAR(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_bin DEFAULT NULL;
-- Only rows without a title are legacy rows. The historical deletion time is unknown: migration time is used.
UPDATE mailmessage SET
  readTime=CASE WHEN status=1 THEN IF(readTime>0,readTime,IF(sendTime>0,sendTime,UNIX_TIMESTAMP())) ELSE GREATEST(readTime,0) END,
  deleteTime=CASE WHEN status=2 AND deleteTime=0 THEN UNIX_TIMESTAMP() ELSE deleteTime END,
  deleteReason=CASE WHEN status=2 AND deleteReason=0 THEN 1 ELSE deleteReason END,
  title='历史邮件'
WHERE title='';
SET @mail_ddl=IF(EXISTS(SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage' AND INDEX_NAME='ix_mail_recipient_active'),'SELECT 1','ALTER TABLE mailmessage ADD INDEX ix_mail_recipient_active(toPlayerId,deleteTime,expireTime)');
PREPARE mail_stmt FROM @mail_ddl;
EXECUTE mail_stmt;
DEALLOCATE PREPARE mail_stmt;
SET @mail_ddl=IF(EXISTS(SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage' AND INDEX_NAME='ix_mail_expiry'),'SELECT 1','ALTER TABLE mailmessage ADD INDEX ix_mail_expiry(deleteTime,expireTime)');
PREPARE mail_stmt FROM @mail_ddl;
EXECUTE mail_stmt;
DEALLOCATE PREPARE mail_stmt;
SET @mail_ddl=IF(EXISTS(SELECT 1 FROM information_schema.STATISTICS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='mailmessage' AND INDEX_NAME='uk_mail_source'),'SELECT 1','ALTER TABLE mailmessage ADD UNIQUE INDEX uk_mail_source(toPlayerId,sourceType,sourceKey)');
PREPARE mail_stmt FROM @mail_ddl;
EXECUTE mail_stmt;
DEALLOCATE PREPARE mail_stmt;

CREATE TABLE IF NOT EXISTS `mailattachment` (
  `mailId` bigint NOT NULL COMMENT '邮件ID',
  `sortOrder` int NOT NULL COMMENT '附件展示顺序，从1开始',
  `itemId` int NOT NULL COMMENT '物品配置ID',
  `quantity` bigint NOT NULL COMMENT '奖励数量',
  PRIMARY KEY (`mailId`,`sortOrder`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='邮件附件快照';
