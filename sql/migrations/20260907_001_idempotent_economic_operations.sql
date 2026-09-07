USE `ugame_10001`;

CREATE TABLE IF NOT EXISTS `player_gacha_operation` (
  `id` bigint(20) NOT NULL,
  `player_id` bigint(20) NOT NULL,
  `operation_id` char(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `pool_id` int(11) NOT NULL,
  `draw_count` int(11) NOT NULL,
  `request_hash` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `result_status` int(11) NOT NULL DEFAULT '-1',
  `batch_id` bigint(20) NOT NULL DEFAULT '0',
  `response_json` longtext NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `completed_at` datetime(6) NOT NULL,
  `expires_at` datetime(6) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_gacha_operation` (`player_id`,`operation_id`),
  KEY `idx_gacha_operation_expire` (`expires_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='抽奖幂等操作';

CREATE TABLE IF NOT EXISTS `player_character_starup_operation` (
  `id` bigint(20) NOT NULL,
  `player_id` bigint(20) NOT NULL,
  `operation_id` char(32) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `character_id` int(11) NOT NULL,
  `target_star` int(11) NOT NULL,
  `request_hash` char(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
  `result_status` int(11) NOT NULL DEFAULT '-1',
  `previous_star` int(11) NOT NULL DEFAULT '0',
  `current_star` int(11) NOT NULL DEFAULT '0',
  `did_upgrade` tinyint(1) NOT NULL DEFAULT '0',
  `used_fragment_num` int(11) NOT NULL DEFAULT '0',
  `remaining_fragment_num` bigint(20) NOT NULL DEFAULT '0',
  `response_json` longtext NOT NULL,
  `created_at` datetime(6) NOT NULL,
  `completed_at` datetime(6) NOT NULL,
  `expires_at` datetime(6) NOT NULL,
  PRIMARY KEY (`id`),
  UNIQUE KEY `uk_character_starup_operation` (`player_id`,`operation_id`),
  KEY `idx_character_starup_operation_expire` (`expires_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COMMENT='角色升星幂等操作';
