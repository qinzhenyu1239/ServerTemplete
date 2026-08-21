using Game.Core.Db;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.GM_Rank
{
    public struct RankData {
        public long uid;
        public int value;
    }

    public class GM_RankMgr
    {
        public static GM_RankMgr Instance = new GM_RankMgr();

        public void Init() { 
        }

        public void FlushRank(int rankType, long playrId, int value) {
            RedisService.Instance.SortedSetAdd(rankType.ToString(), playrId.ToString(), value);
        }

        public RankData[] GetRankData(int rankType, int num) {
            SortedSetEntry[] rankData = RedisService.Instance.SortedSetRangeByRankWithScores(rankType.ToString(), num);
            if (rankData == null || rankData.Length <= 0) {
                return null;
            }

            RankData[] randList = new RankData[rankData.Length];

            for (int i = 0; i < randList.Length; i++) {
                randList[i].uid = long.Parse(rankData[i].Element);
                randList[i].value = (int)rankData[i].Score;
            }
            return randList;

        }
    }
}
