using SqlSugar;

namespace Game.Datas.DBEntities
{
    [SugarTable("playerreddot")]
    public sealed class Playerreddot
    {
        [SugarColumn(IsPrimaryKey = true)] public long playerId { get; set; }
        [SugarColumn(IsPrimaryKey = true)] public int nodeId { get; set; }
        public bool isActive { get; set; }
        public long seenVersion { get; set; }
        public long updateTime { get; set; }
    }
}
