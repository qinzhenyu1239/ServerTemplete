using System;

namespace Game.Datas.DBEntities
{
    public sealed class Player_gacha_operation
    {
        public long id { get; set; }
        public long player_id { get; set; }
        public string operation_id { get; set; }
        public int pool_id { get; set; }
        public int draw_count { get; set; }
        public string request_hash { get; set; }
        public int result_status { get; set; }
        public long batch_id { get; set; }
        public string response_json { get; set; }
        public DateTime created_at { get; set; }
        public DateTime completed_at { get; set; }
        public DateTime expires_at { get; set; }
    }
}
