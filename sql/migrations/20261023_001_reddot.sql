-- Run against the intended ugame database before starting the updated game server.
CREATE TABLE IF NOT EXISTS `playerreddot` (
  `playerId` bigint NOT NULL COMMENT '玩家ID',
  `nodeId` int NOT NULL COMMENT '红点叶子节点ID',
  `isActive` tinyint NOT NULL DEFAULT 0 COMMENT '0隐藏，1显示',
  `seenVersion` bigint NOT NULL DEFAULT 0 COMMENT '已查看提示版本',
  `updateTime` bigint NOT NULL DEFAULT 0 COMMENT '更新时间，Unix秒',
  PRIMARY KEY (`playerId`,`nodeId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='玩家红点状态';

