USE `ugame_10001`;

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
