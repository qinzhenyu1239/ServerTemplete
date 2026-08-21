using Framework.Core.Cache;
using Framework.Core.Utils;
using Game.Datas.DBEntities;
using Game.Datas.Messages;
using Game.Core.Db;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.Caches
{
    class PlayerAcountIDCache : BaseCacheSerivce<long, Game.Datas.DBEntities.Player>
    {
        public static PlayerAcountIDCache Instance = new PlayerAcountIDCache();
        public void Init() { 
        }

        public override Game.Datas.DBEntities.Player Load(long accountIdAndJob)
        {
            long accoutID = (accountIdAndJob >> 3);
            long job = accountIdAndJob & 0x07; // 
            return DBService.Instance.GetGameInstance().Queryable<Game.Datas.DBEntities.Player>().First(it => it.accountId == accoutID);//查询单条
        }

        public long Key(long id, int job) {
            long key = (id << 3) | ((long)0); // 一个账号只允许一个职业;
            // long key = (id << 3) | ((long)job);
            return key;
        }

        public Game.Datas.DBEntities.Player GetOrCreate(long accountId, ReqSelectPlayer req) {
            long accountIdAndJob = this.Key(accountId, req.job); 
            
            Game.Datas.DBEntities.Player dbPlayer = this.Get(accountIdAndJob);
            if (dbPlayer != null) {
                return dbPlayer;
            }

            // 后期我们包一个函数出来，然后专门用来构造Default数据;
            // 根据具体游戏的配置文件来读取，来决定他们的初始化参数;
            dbPlayer = new Game.Datas.DBEntities.Player();
            dbPlayer.id = IdGenerator.GetNextId();
            dbPlayer.accountId = accountId;
            dbPlayer.job = (byte)req.job;
            dbPlayer.name = req.uname;
            dbPlayer.HP = 100;
            dbPlayer.MP = 0;
            dbPlayer.status = 0;
            dbPlayer.lastDailyReset = 0;
            dbPlayer.level = 1;
            dbPlayer.ucoin = 100;
            dbPlayer.umoney = 0;
            dbPlayer.status = 0;
            dbPlayer.usex = req.usex;
            dbPlayer.vipRightJson = "";
            // dbPlayer.usex = req.usex;
            // dbPlayer.charactorId = req.charactorId;

            DBService.Instance.GetGameInstance().Insertable(dbPlayer).ExecuteCommandAsync();
            this.Put(accountIdAndJob, dbPlayer);


            PlayerIDCache.Instance.Put(dbPlayer.id, dbPlayer);

            return dbPlayer;

        }

        public Game.Datas.DBEntities.Player TryGetPlayer(long accountId, int job)
        {
            long accountIdAndJob = this.Key(accountId, job);
            Game.Datas.DBEntities.Player dbPlayer = this.Get(accountIdAndJob);
            if (dbPlayer != null)
            {
                return dbPlayer;
            }

            return null;
        }
    }
}
