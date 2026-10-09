using Framework.Core.Net;
using Game.Core.Configs;
using Game.Core.GM_MailMessage;
using Game.Core.GM_Rank;
using Game.Datas.GMEntities;
using Game.LogicServer;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.EntityMgr 
{
    public class GM_EntityMgr  {
        public static GM_EntityMgr Instance = new GM_EntityMgr();

        // 线程安全;
        private ConcurrentDictionary<long, GM_PlayerEntity> players = null; 

        public void Init() {
            this.players = new ConcurrentDictionary<long, GM_PlayerEntity>();
        }

        public bool BindPlayerSession(IdSession session, long accountId, long playerId, int job) {
            if (session == null || accountId <= 0 || playerId <= 0 || job <= 0 ||
                session.accountId != accountId) {
                return false;
            }

            lock (SessionMgr.Instance.BindingSync)
            {
                if (!SessionMgr.Instance.IsCurrentConnection(session) || session.client != null && !session.client.Active)
                    return false;
                session.playerId = playerId;
                session.accountIdAndJob = GM_AccountEntityMgr.Instance.PlayerKey(accountId, job);
                AddPlayer(session, playerId);
                return true;
            }
        }

        public int? ExitCurrentSession(IdSession session, Func<int> exit)
        {
            lock (SessionMgr.Instance.BindingSync)
            {
                GM_PlayerEntity player = Get(session.playerId);
                if (player == null || !ReferenceEquals(player.uPlayer.s, session)) return null;
                return exit();
            }
        }

        public void ClearDisconnectedSession(IdSession session)
        {
            lock (SessionMgr.Instance.BindingSync)
            {
                GM_PlayerEntity player = Get(session.playerId);
                if (player != null && ReferenceEquals(player.uPlayer.s, session)) player.uPlayer.s = null;
            }
        }

        public int AddPlayer(IdSession s, long playerId) {
            GM_PlayerEntity player = null;
            if (this.players.ContainsKey(playerId)) {
                player = this.players[playerId];
                player.uPlayer.s = s;
                player.uPlayer.playerInfo = GM_AccountEntityMgr.Instance.GetPlayerById(playerId);
                player.uPlayer.accountInfo = GM_AccountEntityMgr.Instance.GetAccountById(player.uPlayer.playerInfo.accountId);

                FieldInfo[] fileds = (typeof(GM_PlayerEntity)).GetFields();
                foreach (var f in fileds)
                {
                    if (f.FieldType == typeof(PlayerComponent))
                    {
                        continue;
                    }

                    MethodInfo m = f.FieldType.GetMethod("Reset", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (m != null)
                    {
                        m.Invoke(null, new object[] { player });
                    }
                }

            }
            else {
                player = new GM_PlayerEntity();
                // 加入到集合;
                this.players.TryAdd(playerId, player);
                // PlayerComponent
                player.uPlayer.playerInfo = GM_AccountEntityMgr.Instance.GetPlayerById(playerId);
                player.uPlayer.accountInfo = GM_AccountEntityMgr.Instance.GetAccountById(player.uPlayer.playerInfo.accountId);
                player.uPlayer.s = s;

                FieldInfo[] fileds = (typeof(GM_PlayerEntity)).GetFields();
                foreach (var f in fileds)
                {
                    if (f.FieldType == typeof(PlayerComponent))
                    {
                        continue;
                    }

                    MethodInfo m = f.FieldType.GetMethod("Init", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (m != null) {
                        m.Invoke(null, new object[] { player });
                    }
                    m = f.FieldType.GetMethod("Reset", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (m != null) {
                        m.Invoke(null, new object[] { player });
                    }
                }
            }

            // 
            // end 

            // 做一个测试,发送一个邮件:
            // GM_MailMsgMgr.Instance.SendMailMsg(-1, playerId, "欢迎来到游戏世界,这是第2封邮件消息测试");
            // end

            // 世界金币排行榜
            if (ConfigMgr.Instance.useRedis) {
                GM_RankMgr.Instance.FlushRank((int)RankType.WorldCoin, playerId, player.uPlayer.playerInfo.ucoin);
            }

            int ret = (player.uRoomGame.playInRoomState == (int)PlayerInRoomState.Playing) ? 1 : 0;

            return ret;
        }


        public void RemovePlayer(long playerId) {
            GM_PlayerEntity player = null;
            this.players.TryRemove(playerId, out player);

            FieldInfo[] fileds = (typeof(GM_PlayerEntity)).GetFields();
            foreach (var f in fileds)
            {
                if (f.FieldType == typeof(PlayerComponent))
                {
                    continue;
                }

                MethodInfo m = f.FieldType.GetMethod("Exit", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                if (m != null)
                {
                    m.Invoke(null, new object[] { player });
                }
            }
        }

        public GM_PlayerEntity Get(long playerId) {
            if (this.players.ContainsKey(playerId)) {
                return this.players[playerId];
            }


            return null;
        }

    }
}
