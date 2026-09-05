-- --------------------------------------------------------
-- 主机:                           127.0.0.1
-- 服务器版本:                        5.7.15-log - MySQL Community Server (GPL)
-- 服务器操作系统:                      Win64
-- HeidiSQL 版本:                  9.4.0.5138
-- --------------------------------------------------------

/*!40101 SET @OLD_CHARACTER_SET_CLIENT=@@CHARACTER_SET_CLIENT */;
/*!40101 SET NAMES utf8 */;
/*!50503 SET NAMES utf8mb4 */;
/*!40014 SET @OLD_FOREIGN_KEY_CHECKS=@@FOREIGN_KEY_CHECKS, FOREIGN_KEY_CHECKS=0 */;
/*!40101 SET @OLD_SQL_MODE=@@SQL_MODE, SQL_MODE='NO_AUTO_VALUE_ON_ZERO' */;


-- 导出 auth_10001 的数据库结构
CREATE DATABASE IF NOT EXISTS `auth_10001` /*!40100 DEFAULT CHARACTER SET utf8 */;
USE `auth_10001`;

-- 导出  表 auth_10001.account 结构
CREATE TABLE IF NOT EXISTS `account` (
  `uid` bigint(20) unsigned NOT NULL COMMENT '玩家唯一的UID号',
  `unick` varchar(32) NOT NULL DEFAULT '""' COMMENT '玩家的昵称',
  `usex` int(8) NOT NULL DEFAULT '0' COMMENT '0:男, 1:女的',
  `uface` int(8) NOT NULL DEFAULT '0' COMMENT '系统默认图像，自定义图像后面再加上',
  `uname` varchar(32) DEFAULT '""' COMMENT '玩家的账号名称',
  `upwd` varchar(32) DEFAULT '""' COMMENT '玩家密码的MD5值',
  `phone` varchar(16) DEFAULT '""' COMMENT '玩家的电话',
  `guest_key` varchar(64) NOT NULL DEFAULT '0' COMMENT '游客账号的唯一的key',
  `email` varchar(64) DEFAULT '""' COMMENT '玩家的email',
  `address` varchar(128) DEFAULT '""' COMMENT '玩家的地址',
  `uvip` int(8) unsigned zerofill NOT NULL DEFAULT '00000000' COMMENT '玩家VIP的等级，这个是最普通的',
  `vip_end_time` int(32) unsigned zerofill NOT NULL DEFAULT '00000000000000000000000000000000' COMMENT '玩家VIP到期的时间撮',
  `is_guest` int(8) unsigned NOT NULL DEFAULT '0' COMMENT '标志改账号是否为游客账号',
  `status` int(8) unsigned zerofill NOT NULL DEFAULT '00000000' COMMENT '0正常，其他的根据需求来定',
  `uchannel` int(8) NOT NULL DEFAULT '0' COMMENT '玩家注册对应的渠道，微信，抖音等',
  PRIMARY KEY (`uid`),
  UNIQUE KEY `uk_account_uname` (`uname`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='存放我们的玩家信息';

-- 导出 ugame_10001 的数据库结构
CREATE DATABASE IF NOT EXISTS `ugame_10001` /*!40100 DEFAULT CHARACTER SET utf8 */;
USE `ugame_10001`;

-- 导出  表 ugame_10001.bonues 结构
CREATE TABLE IF NOT EXISTS `bonues` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '领取奖励的唯一ID号',
  `uid` bigint(20) NOT NULL COMMENT '用户的UID',
  `tid` int(11) NOT NULL DEFAULT '0' COMMENT '奖励对应的类型',
  `status` int(11) NOT NULL DEFAULT '0' COMMENT '是否已经领取, 0未领取，1已领取,2已过期',
  `time` int(11) NOT NULL DEFAULT '0' COMMENT '发放奖励的时间戳',
  `endTime` int(11) NOT NULL DEFAULT '0' COMMENT 'endTime: 奖励结束领取的时间;',
  `bonuesDesic` varchar(64) NOT NULL DEFAULT '  ' COMMENT '发生奖励的原因',
  `bonues1` int(11) NOT NULL DEFAULT '0' COMMENT '第1个奖励数据',
  `bonues2` int(11) NOT NULL DEFAULT '0' COMMENT '第2个奖励数据',
  `bonues3` int(11) NOT NULL DEFAULT '0' COMMENT '第3个奖励数据',
  `bonues4` int(11) NOT NULL DEFAULT '0' COMMENT '第4个奖励数据',
  `bonues5` int(11) NOT NULL DEFAULT '0' COMMENT '第5个奖励数据',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='玩家奖励管理';

-- 导出  表 ugame_10001.gamegoods 结构
CREATE TABLE IF NOT EXISTS `gamegoods` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '物品实例ID号',
  `uid` bigint(20) NOT NULL COMMENT '用户的UID',
  `tid` int(11) NOT NULL DEFAULT '0' COMMENT '物品的类型ID，关联背包物品的配置表',
  `num` int(11) NOT NULL DEFAULT '0' COMMENT '背包物品的数目',
  `strengData` varbinary(64) DEFAULT '0' COMMENT '可选，不同游戏，可能不一样,保存当前装备的强化数据,可以考虑使用protobuf序列化',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='玩家当前的任务实例列表';

-- 导出  表 ugame_10001.gametask 结构
CREATE TABLE IF NOT EXISTS `gametask` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '任务实例ID号',
  `uid` bigint(20) NOT NULL COMMENT '用户的UID',
  `tid` int(11) NOT NULL DEFAULT '0' COMMENT '任务类型ID号',
  `status` int(11) NOT NULL DEFAULT '0' COMMENT '当前任务的状态,0未开启,1进行中，2任务完成,3任务取消等',
  `startTime` int(11) NOT NULL DEFAULT '0' COMMENT '开始任务的时间戳',
  `endTime` int(11) NOT NULL DEFAULT '0' COMMENT '任务结束的时间戳，-1长期有效',
  `TaskData` varbinary(64) NOT NULL DEFAULT '0' COMMENT '保存当前任务的数据进度，Protobuf编码',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='玩家当前的任务实例列表';

-- 导出  表 ugame_10001.loginbonues 结构
CREATE TABLE IF NOT EXISTS `loginbonues` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '领取奖励的唯一ID号',
  `uid` bigint(20) NOT NULL COMMENT '用户的UID',
  `bonues` int(11) NOT NULL DEFAULT '0' COMMENT '奖励的数目',
  `status` int(11) NOT NULL DEFAULT '0' COMMENT '是否已经领取, 0未领取，1已领取',
  `bonues_time` int(11) NOT NULL DEFAULT '0' COMMENT '发放奖励的时间',
  `days` int(11) NOT NULL DEFAULT '0' COMMENT '连续登陆天数',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='登陆奖励管理';

-- 导出  表 ugame_10001.mailmessage 结构
CREATE TABLE IF NOT EXISTS `mailmessage` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '邮件消息的唯一ID号',
  `fromPlayerId` bigint(20) NOT NULL DEFAULT '-1' COMMENT '来自与哪个玩家Id发送的邮件',
  `toPlayerId` bigint(20) NOT NULL DEFAULT '0' COMMENT '发送给哪个玩家Id的邮件',
  `msgBody` varchar(256) NOT NULL DEFAULT '' COMMENT '邮件消息主题内容',
  `status` int(11) NOT NULL DEFAULT '0' COMMENT '当前邮件消息的状态，0，表示未读， 1表示已读，2表示已删除;',
  `sendTime` int(11) NOT NULL DEFAULT '0' COMMENT '邮件消息发送的时间',
  `userData` bigint(20) NOT NULL DEFAULT '0' COMMENT '消息邮件要关联的一些用户数据，具体可以根据游戏项目来定,暂定用BigInt,带任务ID，奖励ID等',
  `readTime` int(11) NOT NULL DEFAULT '0' COMMENT '邮件消息被玩家阅读的时间',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='邮件消息管理';

-- 导出  表 ugame_10001.player 结构
CREATE TABLE IF NOT EXISTS `player` (
  `id` bigint(20) NOT NULL DEFAULT '10000' COMMENT 'playerId',
  `accountId` bigint(20) NOT NULL DEFAULT '0' COMMENT '账号ID',
  `level` int(11) NOT NULL DEFAULT '1' COMMENT '等级',
  `name` varchar(128) DEFAULT NULL COMMENT '用户名字',
  `usex` int(11) NOT NULL DEFAULT '0',
  `job` int(11) NOT NULL DEFAULT '0' COMMENT '职业',
  `exp` int(11) NOT NULL DEFAULT '0' COMMENT '经验',
  `HP` int(11) NOT NULL DEFAULT '0' COMMENT '玩家当前的HP',
  `MP` int(11) NOT NULL DEFAULT '0' COMMENT '玩家当前的MP',
  `equipment` varchar(128) DEFAULT NULL COMMENT '装备情况',
  `ucoin` int(11) NOT NULL DEFAULT '0' COMMENT '玩家的金币,游戏，奖励，卖装备获取, 购买消耗品',
  `umoney` int(11) NOT NULL DEFAULT '0' COMMENT '玩家的元宝, 充值可以获得',
  `lastDailyReset` bigint(255) DEFAULT NULL,
  `vipRightJson` varchar(255) DEFAULT NULL COMMENT 'VIP特权',
  `platform` varchar(255) DEFAULT NULL COMMENT '用户在哪个平台',
  `status` int(11) NOT NULL DEFAULT '0' COMMENT '账号的状态',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC;

-- 抽奖使用的玩家角色、道具、保底和记录表。全部使用 InnoDB，确保一次抽奖可原子提交。
CREATE TABLE IF NOT EXISTS `player_item` (
  `id` bigint(20) unsigned NOT NULL AUTO_INCREMENT COMMENT '记录ID',
  `player_id` bigint(20) NOT NULL COMMENT '玩家ID',
  `item_id` int(11) NOT NULL COMMENT 'Item配置表ID',
  `quantity` bigint(20) unsigned NOT NULL DEFAULT '0' COMMENT '拥有数量',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_player_item` (`player_id`,`item_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='玩家道具背包';

CREATE TABLE IF NOT EXISTS `player_character` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '角色实例ID',
  `player_id` bigint(20) NOT NULL COMMENT '玩家ID',
  `character_id` int(11) NOT NULL COMMENT '角色配置ID',
  `star` tinyint(3) unsigned NOT NULL DEFAULT '0' COMMENT '角色星级',
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_player_character` (`player_id`,`character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='玩家拥有的角色';

CREATE TABLE IF NOT EXISTS `player_gacha_state` (
  `player_id` bigint(20) NOT NULL COMMENT '玩家ID',
  `pool_id` int(11) NOT NULL COMMENT '卡池ID',
  `small_pity_count` int(10) unsigned NOT NULL DEFAULT '0',
  `big_pity_count` int(10) unsigned NOT NULL DEFAULT '0',
  `total_draw_count` bigint(20) unsigned NOT NULL DEFAULT '0',
  PRIMARY KEY (`player_id`,`pool_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='玩家各卡池的抽奖保底状态';

CREATE TABLE IF NOT EXISTS `player_gacha_record` (
  `id` bigint(20) NOT NULL COMMENT '抽奖记录ID',
  `batch_id` bigint(20) NOT NULL COMMENT '抽奖批次ID',
  `draw_index` tinyint(3) unsigned NOT NULL DEFAULT '1',
  `player_id` bigint(20) NOT NULL,
  `pool_id` int(11) NOT NULL,
  `character_id` int(11) NOT NULL,
  `rarity` tinyint(3) unsigned NOT NULL DEFAULT '1',
  `is_new_character` tinyint(1) NOT NULL DEFAULT '0',
  `fragment_item_id` int(11) NOT NULL DEFAULT '0',
  `fragment_num` int(10) unsigned NOT NULL DEFAULT '0',
  `small_pity_count` int(10) unsigned NOT NULL DEFAULT '0',
  `big_pity_count` int(10) unsigned NOT NULL DEFAULT '0',
  `created_at` datetime NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_gacha_batch_draw` (`batch_id`,`draw_index`),
  KEY `idx_gacha_player_created_id` (`player_id`,`created_at`,`id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='玩家角色抽奖记录';

-- 兼容已有数据库：CREATE TABLE IF NOT EXISTS 不会为旧表补充新索引，
-- 因此在索引不存在时执行一次在线 ALTER；重复执行本文件不会重复创建。
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

/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
