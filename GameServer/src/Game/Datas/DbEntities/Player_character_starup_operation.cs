using System;

namespace Game.Datas.DBEntities
{
    public sealed class Player_character_starup_operation
    {
        public long id { get; set; }
        public long player_id { get; set; }
        public string operation_id { get; set; }
        public int character_id { get; set; }
        public int target_star { get; set; }
        public string request_hash { get; set; }
        public int result_status { get; set; }
        public int previous_star { get; set; }
        public int current_star { get; set; }
        public bool did_upgrade { get; set; }
        public int used_fragment_num { get; set; }
        public long remaining_fragment_num { get; set; }
        public string response_json { get; set; }
        public DateTime created_at { get; set; }
        public DateTime completed_at { get; set; }
        public DateTime expires_at { get; set; }
    }
}
