using SqlSugar;
namespace Game.Datas.DBEntities
{
    [SugarTable("mailmessage")]
    public sealed class Mailmessage
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true)] public long id { get; set; }
        public long fromPlayerId { get; set; }
        public long toPlayerId { get; set; }
        public int mailType { get; set; }
        public string title { get; set; }
        public string msgBody { get; set; }
        public int sourceType { get; set; }
        [SugarColumn(IsNullable = true)] public string sourceKey { get; set; }
        public long sendTime { get; set; }
        public long expireTime { get; set; }
        public long readTime { get; set; }
        public long claimTime { get; set; }
        public long deleteTime { get; set; }
        public int deleteReason { get; set; }
        // Retained only for old database compatibility. New logic does not use these fields.
        public int status { get; set; }
        public long userData { get; set; }
    }
}
