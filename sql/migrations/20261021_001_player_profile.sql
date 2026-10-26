USE `ugame_10001`;

-- 在部署包含这些字段的服务端之前执行一次。
-- 历史角色的原创建时间无法推断，保留0表示未知；新角色由服务端写入Unix秒时间戳。
ALTER TABLE `player`
  ADD COLUMN `avatarId` int(11) NOT NULL DEFAULT '0' COMMENT '头像ID，0表示未设置' AFTER `name`,
  ADD COLUMN `guildId` bigint(20) NOT NULL DEFAULT '0' COMMENT '公会ID，0表示未加入公会' AFTER `avatarId`,
  ADD COLUMN `titleId` int(11) NOT NULL DEFAULT '0' COMMENT '称号ID，0表示未佩戴称号' AFTER `guildId`,
  ADD COLUMN `signature` varchar(255) CHARACTER SET utf8mb4 NOT NULL DEFAULT '' COMMENT '个性签名' AFTER `titleId`,
  ADD COLUMN `createTime` bigint(20) NOT NULL DEFAULT '0' COMMENT '创建角色Unix秒时间戳，0表示未知' AFTER `signature`;
