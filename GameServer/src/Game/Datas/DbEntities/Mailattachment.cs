using SqlSugar;
namespace Game.Datas.DBEntities
{
    [SugarTable("mailattachment")]
    public sealed class Mailattachment
    {
        [SugarColumn(IsPrimaryKey = true)] public long mailId { get; set; }
        [SugarColumn(IsPrimaryKey = true)] public int sortOrder { get; set; }
        public int itemId { get; set; }
        public long quantity { get; set; }
    }
}
