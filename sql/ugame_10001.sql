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

-- 正在导出表  auth_10001.account 的数据：~8 rows (大约)
/*!40000 ALTER TABLE `account` DISABLE KEYS */;
REPLACE INTO `account` (`uid`, `unick`, `usex`, `uface`, `uname`, `upwd`, `phone`, `guest_key`, `email`, `address`, `uvip`, `vip_end_time`, `is_guest`, `status`, `uchannel`) VALUES
	(281869840390160385, '游客3437', 0, 4, '18275133532', '202CB962AC59075B964B07152D234B70', NULL, '', NULL, NULL, 00000000, 00000000000000000000000000000000, 0, 00000000, 0),
	(281869840703422465, '游客5414', 1, 1, '18375133532', 'E10ADC3949BA59ABBE56E057F20F883E', NULL, '', NULL, NULL, 00000000, 00000000000000000000000000000000, 0, 00000000, 0),
	(281869880750571521, '用户8404', 1, 1, '18175133532', '202CB962AC59075B964B07152D234B70', NULL, '', NULL, NULL, 00000000, 00000000000000000000000000000000, 0, 00000000, 0),
	(281869881062391809, '用户3134', 1, 1, '18075133532', 'E10ADC3949BA59ABBE56E057F20F883E', NULL, '', NULL, NULL, 00000000, 00000000000000000000000000000000, 0, 00000000, 0),
	(281869886125178881, '游客5081', 0, 3, NULL, NULL, NULL, '1zbGSuAWkwfhXsL68VUOp3UX5fx5ek2K', NULL, NULL, 00000000, 00000000000000000000000000000000, 1, 00000000, 0),
	(281869886411898881, '游客5658', 1, 1, '18475133532', 'E10ADC3949BA59ABBE56E057F20F883E', NULL, '', NULL, NULL, 00000000, 00000000000000000000000000000000, 0, 00000000, 0),
	(281869886417010691, '游客6226', 0, 4, '18575133532', 'E10ADC3949BA59ABBE56E057F20F883E', NULL, '', NULL, NULL, 00000000, 00000000000000000000000000000000, 0, 00000000, 0),
	(281869886426710021, '游客1030', 1, 0, NULL, NULL, NULL, 'NAYr7J5ZPZ38yKA373cdBt2bSTQkBxHx', NULL, NULL, 00000000, 00000000000000000000000000000000, 1, 00000000, 0),
	(281870114213134337, '游客2137', 0, 3, NULL, NULL, NULL, 'pd6FSbGyRXHXYNaSdDRs7yDExFDFiiNB', NULL, NULL, 00000000, 00000000000000000000000000000000, 1, 00000000, 0),
	(281870119074004993, '游客1031', 1, 2, NULL, NULL, NULL, 'tmYxzCfxXzjaLmyslyNrrxolNrzFLhEh', NULL, NULL, 00000000, 00000000000000000000000000000000, 1, 00000000, 0);
/*!40000 ALTER TABLE `account` ENABLE KEYS */;


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
) ENGINE=InnoDB AUTO_INCREMENT=21 DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='玩家奖励管理';

-- 正在导出表  ugame_10001.bonues 的数据：~18 rows (大约)
/*!40000 ALTER TABLE `bonues` DISABLE KEYS */;
REPLACE INTO `bonues` (`id`, `uid`, `tid`, `status`, `time`, `endTime`, `bonuesDesic`, `bonues1`, `bonues2`, `bonues3`, `bonues4`, `bonues5`) VALUES
	(1, 281869886126227458, 100002, 1, 1731811822, -1, '测试,每次登录送可以领取10Monney。', 10, 0, 0, 0, 0),
	(2, 281869886126227458, 100002, 1, 1731811905, -1, '测试,每次登录送可以领取10Monney。', 10, 0, 0, 0, 0),
	(3, 281869886126227458, 100002, 1, 1731811943, -1, '测试,每次登录送可以领取10Monney。', 10, 0, 0, 0, 0),
	(4, 281869886126227458, 100002, 1, 1731812004, -1, '测试,每次登录送可以领取10Monney。', 10, 0, 0, 0, 0),
	(5, 281869886126227458, 100002, 1, 1731812026, -1, '测试,每次登录送可以领取10Monney。', 10, 0, 0, 0, 0),
	(6, 281869886427365382, 100002, 1, 1731826161, -1, '测试,每次登录送可以领取10Monney。', 10, 0, 0, 0, 0),
	(7, 281869886427365382, 100002, 1, 1731826181, -1, '测试,每次登录送可以领取10Monney。', 10, 0, 0, 0, 0),
	(8, 281869886427365382, 100002, 1, 1731826194, -1, '测试,每次登录送可以领取10Monney。', 10, 0, 0, 0, 0),
	(9, 281869886427365382, 100002, 1, 1731826201, -1, '测试,每次登录送可以领取10Monney。', 10, 0, 0, 0, 0),
	(10, 281869886427365382, 100002, 1, 1731826208, -1, '测试,每次登录送可以领取10Monney。', 10, 0, 0, 0, 0),
	(11, 281869886427365382, 100001, 1, 1731827522, -1, '游客账号升级成功，可以领取180金币', 180, 0, 0, 0, 0),
	(12, 281869886427365382, 100001, 1, 1731827632, -1, '游客账号升级成功，可以领取180金币', 180, 0, 0, 0, 0),
	(13, 281869886427365382, 100001, 1, 1731827716, -1, '游客账号升级成功，可以领取180金币', 180, 0, 0, 0, 0),
	(14, 281869886427365382, 100001, 0, 1731829318, -1, '游客账号升级成功，可以领取180金币', 180, 0, 0, 0, 0),
	(15, 281869886126227458, 100001, 1, 1731895695, -1, '游客账号升级成功，可以领取180金币', 180, 0, 0, 0, 0),
	(16, 281869886427365382, 100001, 0, 1731900714, -1, '游客账号升级成功，可以领取180金币', 180, 0, 0, 0, 0),
	(17, 281869886126227458, 100001, 0, 1731918204, -1, '游客账号升级成功，可以领取180金币', 180, 0, 0, 0, 0),
	(18, 281869886126227458, 100001, 0, 1731920297, -1, '数据来自表格:游客账号升级成功，可以领取170金币', 170, 0, 0, 0, 0),
	(19, 281869881414057985, 100001, 1, 1738844337, -1, '数据来自表格:游客账号升级成功，可以领取170金币', 170, 0, 0, 0, 0),
	(20, 281869881414057985, 100002, 1, 1738844413, -1, '数据来自表格_测试，每次登录可送10钻石', 10, 0, 0, 0, 0);
/*!40000 ALTER TABLE `bonues` ENABLE KEYS */;

-- 导出  表 ugame_10001.gamegoods 结构
CREATE TABLE IF NOT EXISTS `gamegoods` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '物品实例ID号',
  `uid` bigint(20) NOT NULL COMMENT '用户的UID',
  `tid` int(11) NOT NULL DEFAULT '0' COMMENT '物品的类型ID，关联背包物品的配置表',
  `num` int(11) NOT NULL DEFAULT '0' COMMENT '背包物品的数目',
  `strengData` varbinary(64) DEFAULT '0' COMMENT '可选，不同游戏，可能不一样,保存当前装备的强化数据,可以考虑使用protobuf序列化',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=10 DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='玩家当前的任务实例列表';

-- 正在导出表  ugame_10001.gamegoods 的数据：~8 rows (大约)
/*!40000 ALTER TABLE `gamegoods` DISABLE KEYS */;
REPLACE INTO `gamegoods` (`id`, `uid`, `tid`, `num`, `strengData`) VALUES
	(1, 281869881414057985, 10003, 5, NULL),
	(2, 281869881414057985, 10004, 6, NULL),
	(3, 281869881414057985, 20003, 1, NULL),
	(4, 281869881414057985, 20002, 7, NULL),
	(5, 281869881414057985, 30003, 1, NULL),
	(6, 281869881414057985, 30004, 1, NULL),
	(7, 281869881414057985, 30006, 1, NULL),
	(8, 281869881414057985, 10001, 15, NULL),
	(9, 281869881414057985, 10002, 5, NULL);
/*!40000 ALTER TABLE `gamegoods` ENABLE KEYS */;

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
) ENGINE=InnoDB AUTO_INCREMENT=281870407676788738 DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='玩家当前的任务实例列表';

-- 正在导出表  ugame_10001.gametask 的数据：~5 rows (大约)
/*!40000 ALTER TABLE `gametask` DISABLE KEYS */;
REPLACE INTO `gametask` (`id`, `uid`, `tid`, `status`, `startTime`, `endTime`, `TaskData`) VALUES
	(281870001097539585, 281869886126227458, 100001, 3, 1732626498, -1, _binary 0x08021000),
	(281870001101602818, 281869886126227458, 100002, 1, 1732626560, -1, _binary 0x08001000),
	(281870001249845249, 281869886427365382, 100001, 3, 1732628822, -1, _binary 0x08021000),
	(281870001311514625, 281869886427365382, 100002, 3, 1732629763, -1, _binary 0x08031003),
	(281870061166395393, 281869881414057985, 100001, 3, 1733543076, -1, _binary 0x08021000),
	(281870114213396483, 281870114213396482, 100001, 3, 1734352509, -1, _binary 0x08021000),
	(281870119074332675, 281870119074332674, 100001, 1, 1734426681, -1, _binary 0x08001000),
	(281870338625961985, 281870114213396482, 100002, 1, 1737805573, -1, _binary 0x08011000),
	(281870407676788737, 281869881414057985, 100002, 1, 1738859205, -1, _binary 0x08001000);
/*!40000 ALTER TABLE `gametask` ENABLE KEYS */;

-- 导出  表 ugame_10001.loginbonues 结构
CREATE TABLE IF NOT EXISTS `loginbonues` (
  `id` bigint(20) NOT NULL AUTO_INCREMENT COMMENT '领取奖励的唯一ID号',
  `uid` bigint(20) NOT NULL COMMENT '用户的UID',
  `bonues` int(11) NOT NULL DEFAULT '0' COMMENT '奖励的数目',
  `status` int(11) NOT NULL DEFAULT '0' COMMENT '是否已经领取, 0未领取，1已领取',
  `bonues_time` int(11) NOT NULL DEFAULT '0' COMMENT '发放奖励的时间',
  `days` int(11) NOT NULL DEFAULT '0' COMMENT '连续登陆天数',
  PRIMARY KEY (`id`)
) ENGINE=InnoDB AUTO_INCREMENT=22 DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='登陆奖励管理';

-- 正在导出表  ugame_10001.loginbonues 的数据：~3 rows (大约)
/*!40000 ALTER TABLE `loginbonues` DISABLE KEYS */;
REPLACE INTO `loginbonues` (`id`, `uid`, `bonues`, `status`, `bonues_time`, `days`) VALUES
	(17, 281869886427365382, 0, 1, 1733543862, 1),
	(18, 281869886126227458, 100, 0, 1733542477, 1),
	(19, 281869881414057985, 0, 1, 1739975741, 2),
	(20, 281870114213396482, 0, 1, 1740097113, 5),
	(21, 281870119074332674, 0, 1, 1740096799, 6);
/*!40000 ALTER TABLE `loginbonues` ENABLE KEYS */;

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
) ENGINE=InnoDB AUTO_INCREMENT=4 DEFAULT CHARSET=utf8 ROW_FORMAT=DYNAMIC COMMENT='邮件消息管理';

-- 正在导出表  ugame_10001.mailmessage 的数据：~3 rows (大约)
/*!40000 ALTER TABLE `mailmessage` DISABLE KEYS */;
REPLACE INTO `mailmessage` (`id`, `fromPlayerId`, `toPlayerId`, `msgBody`, `status`, `sendTime`, `userData`, `readTime`) VALUES
	(1, -1, 281869886126227458, '欢迎来到游戏世界,这是邮件消息测试', 1, 1732870413, 0, 1732871019),
	(2, -1, 281869886427365382, '欢迎来到游戏世界,这是邮件消息测试', 0, 1732873592, 0, -1),
	(3, -1, 281869886427365382, '欢迎来到游戏世界,这是邮件消息测试', 1, 1732873611, 0, 1732874055);
/*!40000 ALTER TABLE `mailmessage` ENABLE KEYS */;

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

-- 正在导出表  ugame_10001.player 的数据：~8 rows (大约)
/*!40000 ALTER TABLE `player` DISABLE KEYS */;
REPLACE INTO `player` (`id`, `accountId`, `level`, `name`, `usex`, `job`, `exp`, `HP`, `MP`, `equipment`, `ucoin`, `umoney`, `lastDailyReset`, `vipRightJson`, `platform`, `status`) VALUES
	(281869841750622209, 281869840703422465, 1, 'CCCBycwtXn3', 0, 2, 0, 100, 0, NULL, 100, 0, 0, '', NULL, 0),
	(281869845408776193, 281869840390160385, 1, 'Bycweh2zj', 0, 2, 0, 100, 0, NULL, 100, 0, 0, '', NULL, 0),
	(281869881414057985, 281869880750571521, 1, 'BycwiqUrW', 0, 2, 1000, 100, 0, NULL, 670, 1, 0, '', NULL, 0),
	(281869881593298945, 281869881062391809, 1, 'CCCBycwGbCN', 1, 3, 0, 100, 0, NULL, 100, 0, 0, '', NULL, 0),
	(281869886126227458, 281869886125178881, 1, 'Rm25C', 0, 2, 1000, 100, 0, NULL, 1480, 130, 0, '', NULL, 0),
	(281869886412816386, 281869886411898881, 1, 'GbaR', 1, 3, 0, 100, 0, NULL, 100, 0, 0, '', NULL, 0),
	(281869886417797124, 281869886417010691, 1, 'simon', 0, 2, 0, 100, 0, NULL, 100, 0, 0, '', NULL, 0),
	(281869886427365382, 281869886426710021, 1, 'WW', 1, 2, 3000, 100, 0, NULL, 1350, 60, 0, '', NULL, 0),
	(281870114213396482, 281870114213134337, 1, 'CCCBycwSW8f', 1, 3, 1000, 100, 0, NULL, 5600, 0, 0, '', NULL, 0),
	(281870119074332674, 281870119074004993, 1, 'Bycw4TheQ', 1, 3, 0, 100, 0, NULL, 4600, 0, 0, '', NULL, 0);
/*!40000 ALTER TABLE `player` ENABLE KEYS */;

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
  KEY `idx_gacha_player_time` (`player_id`,`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='玩家角色抽奖记录';

/*!40101 SET SQL_MODE=IFNULL(@OLD_SQL_MODE, '') */;
/*!40014 SET FOREIGN_KEY_CHECKS=IF(@OLD_FOREIGN_KEY_CHECKS IS NULL, 1, @OLD_FOREIGN_KEY_CHECKS) */;
/*!40101 SET CHARACTER_SET_CLIENT=@OLD_CHARACTER_SET_CLIENT */;
