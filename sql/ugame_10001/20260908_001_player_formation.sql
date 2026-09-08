-- 玩家编队：每个模块有 formation_id 0-4 共五套编队。
-- 可重复执行；业务层负责 module_id、formation_id、人数和角色归属校验。
CREATE TABLE IF NOT EXISTS `player_formation` (
  `player_id` bigint(20) NOT NULL COMMENT '玩家ID',
  `module_id` tinyint(3) unsigned NOT NULL COMMENT '玩法模块ID：0、1、2',
  `formation_id` tinyint(3) unsigned NOT NULL COMMENT '编队ID：0-4',
  `version` bigint(20) unsigned NOT NULL DEFAULT '0' COMMENT '乐观锁版本',
  `created_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  `updated_at` datetime(6) NOT NULL DEFAULT CURRENT_TIMESTAMP(6),
  PRIMARY KEY (`player_id`,`module_id`,`formation_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='玩家编队版本';

CREATE TABLE IF NOT EXISTS `player_formation_slot` (
  `player_id` bigint(20) NOT NULL COMMENT '玩家ID',
  `module_id` tinyint(3) unsigned NOT NULL COMMENT '玩法模块ID：0、1、2',
  `formation_id` tinyint(3) unsigned NOT NULL COMMENT '编队ID：0-4',
  `slot_index` tinyint(3) unsigned NOT NULL COMMENT '零基槽位序号',
  `character_id` int(11) NOT NULL COMMENT 'Character配置ID',
  PRIMARY KEY (`player_id`,`module_id`,`formation_id`,`slot_index`),
  UNIQUE KEY `uk_player_formation_character` (`player_id`,`module_id`,`formation_id`,`character_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='玩家编队槽位';
