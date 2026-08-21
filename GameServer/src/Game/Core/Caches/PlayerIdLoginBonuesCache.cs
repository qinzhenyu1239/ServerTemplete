using Framework.Core.Cache;
using Framework.Core.Utils;
using Game.Datas.DBEntities;
using Game.Core.Db;


namespace Game.Core.Caches
{
    class PlayerIdLoginBonuesCache : BaseCacheSerivce<long, Game.Datas.DBEntities.Loginbonues>
    {
        public static PlayerIdLoginBonuesCache Instance = new PlayerIdLoginBonuesCache();
        public void Init()
        {
        }

        public override Loginbonues Load(long playerID)
        {
            return DBService.Instance.GetGameInstance().Queryable<Game.Datas.DBEntities.Loginbonues>().First(it => it.uid == playerID);//查询单条
        }

        public void UpdateDataToDb(Game.Datas.DBEntities.Loginbonues dbData) {
            DBService.Instance.GetGameInstance().Updateable(dbData).Where(it => it.uid == dbData.uid).ExecuteCommandAsync();
        }

        public Game.Datas.DBEntities.Loginbonues GetOrCreate(long playerId)
        {
            Game.Datas.DBEntities.Loginbonues dbLoginBonues = this.Get(playerId);
            if (dbLoginBonues != null) {
                return dbLoginBonues;
            }

            // 后期我们包一个函数出来，然后专门用来构造Default数据;
            // 根据具体游戏的配置文件来读取，来决定他们的初始化参数;
            dbLoginBonues = new Game.Datas.DBEntities.Loginbonues();
            // dbLoginBonues.id = IdGenerator.GetNextId();
            dbLoginBonues.uid = playerId;
            dbLoginBonues.bonues = 0;
            dbLoginBonues.status = 1;
            dbLoginBonues.bonues_time = (int)(UtilsHelper.Timestamp());
            dbLoginBonues.days = 0;

            DBService.Instance.GetGameInstance().Insertable(dbLoginBonues).ExecuteCommandAsync();
            this.Put(playerId, dbLoginBonues);


            return dbLoginBonues;

        }
    }
}
