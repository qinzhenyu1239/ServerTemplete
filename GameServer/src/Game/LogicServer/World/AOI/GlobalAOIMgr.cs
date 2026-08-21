using Framework.Core.Serializer;

using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.LogicServer
{
    public class GlobalAOIMgr : AOIMgr
    {
        // 全局AOI，一次管理所有的角色
        private Dictionary<long, GM_PlayerEntity> players = null;

        public override void Init(IMapWrapper mapWrapper, int viewSize = 0) {
            base.Init(mapWrapper, viewSize);

            this.players = new Dictionary<long, GM_PlayerEntity>();
        }

        public override void BroadMsgToAOI(BaseEntity center, Message s, long ignorePlayerId = -1, List<GM_PlayerEntity> toPlayers = null)
        {
            foreach (var playerId in this.players.Keys)
            {
                if (playerId == ignorePlayerId)
                {
                    continue;
                }

                this.SendMsg(this.players[playerId], s);
            }
        }

        public override void EnterToAOI(GM_PlayerEntity e)
        {

            long playerId = e.uPlayer.playerInfo.id;

            this.players.Add(playerId, e);

            // 通知周围所有的玩家客户端,你进入到了视野中
            ResEnterAOI res = new ResEnterAOI();
            List<ArrivedCharactor> arrivedCharactors = new List<ArrivedCharactor>();
            ArrivedCharactor ch = this.GetArrivedCharactor(e);
            arrivedCharactors.Add(ch);
            res.charactors = arrivedCharactors.ToArray();

            this.BroadMsgToAOI(e, res, playerId); // 忽略自己;
            // end

            // 把周围所有玩家的数据发送给你，告诉你，它们也进入了你的视野;
            arrivedCharactors = new List<ArrivedCharactor>();
            res = new ResEnterAOI();

            foreach (var entity in this.players.Values)
            {
                ch = this.GetArrivedCharactor(entity);
                arrivedCharactors.Add(ch);
            }
            // end

            res.charactors = arrivedCharactors.ToArray();
            this.SendMsg(e, res);
        }

        public override void LeaveFromAOI(GM_PlayerEntity e)
        {
            long playerId = e.uPlayer.playerInfo.id;
            if (!this.players.ContainsKey(playerId))
            {
                return;
            }

            this.players.Remove(playerId);

            ResLeaveAOI res = new ResLeaveAOI();
            res.leavePlayers = new int[1]; // 当我们一个块进入视野，一个块在视野消失,还是用数组;
            res.leavePlayers[0] = e.uWorldGame.worldId;

            this.BroadMsgToAOI(e, res, playerId);
        }

        private void OnNavSystemUpdate(float dt)
        {
            foreach (var e in this.players.Values)
            {
                if (e.uStatus.status == (int)CharactorStatus.Run)
                {
                    this.mapWrapper.OnNavUpdate(this, e, dt);
                }
            }
        }

        private void OnNavSystemLateUpdate(float dt)
        {
            foreach (var e in this.players.Values)
            {
                if (e.uStatus.status == (int)CharactorStatus.Run)
                {
                    this.mapWrapper.OnNavLateUpdate(this, e, dt);
                }
            }
        }

        private void OnSkillAndBuffUpdate(float dt)
        {
            foreach (var e in this.players.Values)
            {
                SkillAndBuffSystem.Update(this, e, dt);
            }
        }

        public override void OnUpdate(float dt)
        {
            // 其它System的迭代;
            // end

            // NavSystem迭代
            this.OnNavSystemUpdate(dt);
            // end

            // SkillAndBuff迭代
            this.OnSkillAndBuffUpdate(dt);
            // end


        }

        public override void OnLateUpdate(float dt)
        {
            // NavSystem迭代
            this.OnNavSystemLateUpdate(dt);
            // end
        }
    }
}
