using Framework.Core.Cache;
using Game.Datas.DBEntities;
using Game.Core.Db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.Caches
{
    class PlayerIDCache : BaseCacheSerivce<long, Game.Datas.DBEntities.Player>
    {
        public static PlayerIDCache Instance = new PlayerIDCache();
        public void Init() { 
        }

        public override Game.Datas.DBEntities.Player Load(long playerID)
        {
            return DBService.Instance.GetGameInstance().Queryable<Game.Datas.DBEntities.Player>().First(it => it.id == playerID);//查询单条
        }

        public void UpdateDataToDb(Game.Datas.DBEntities.Player dbData)
        {
            DBService.Instance.GetGameInstance().Updateable(dbData).Where(it => it.id == dbData.id).ExecuteCommandAsync();
        }

    }
}
