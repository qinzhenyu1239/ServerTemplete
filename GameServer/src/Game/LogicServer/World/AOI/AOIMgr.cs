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

    public abstract class AOIMgr {

        
        protected IMapWrapper mapWrapper = null;

        public virtual void Init(IMapWrapper mapWrapper, int viewSize = 0) {
            this.mapWrapper = mapWrapper;
        }

        public void SendMsg(long playerId, Message m)
        {
            GM_PlayerEntity e = GM_EntityMgr.Instance.Get(playerId);
            if (e == null || e.uPlayer.s == null || e.uWorldGame.reconnectState != -1)
            {
                return;
            }

            MessagePusher.PushMessage(e.uPlayer.s, m, playerId);
        }

        public void SendMsg(GM_PlayerEntity e, Message m)
        {
            if (e.uPlayer.s == null || e.uWorldGame.reconnectState != -1)
            {
                return;
            }

            MessagePusher.PushMessage(e.uPlayer.s, m, e.uPlayer.playerInfo.id);
        }

        public abstract void BroadMsgToAOI(BaseEntity center, Message s, long ignorePlayerId = -1, List<GM_PlayerEntity> toPlayers = null);

        protected ArrivedCharactor GetArrivedCharactor(GM_PlayerEntity e) {
            ArrivedCharactor ch = new ArrivedCharactor();

            ch.worldId = e.uWorldGame.worldId;

            // 
            ch.chInfo = new CharactorInfo();
            ch.chInfo.unick = e.uPlayer.playerInfo.name;
            ch.chInfo.job = e.uPlayer.playerInfo.job;
            ch.chInfo.charactorId = -1;  // 保留，如果你后续有需求, 数据库保留这个角色ID就可以了;
            ch.chInfo.sex = e.uPlayer.playerInfo.usex;

            // Transform
            ch.transform = new CharactorTransform();
            ch.transform.pos = new float[3];
            ch.transform.pos[0] = e.uTransform.pos.x;
            ch.transform.pos[1] = e.uTransform.pos.y;
            ch.transform.pos[2] = e.uTransform.pos.z;

            ch.transform.eulerAngles = new float[3];
            ch.transform.eulerAngles[0] = e.uTransform.eulerAngles.x;
            ch.transform.eulerAngles[1] = e.uTransform.eulerAngles.y;
            ch.transform.eulerAngles[2] = e.uTransform.eulerAngles.z;

            // status
            ch.status = e.uStatus.status;

            // 战斗数据...


            return ch;
        }

        /*
        有更多的Entity,都要写好，放到AOI里面来管理;
        abstract public void EnterToAOI(GM_MonestEntity e);
        abstract public void LeaveFromAOI(GM_MonestEntity e);
         ...
        */

        abstract public void EnterToAOI(GM_PlayerEntity e);
        abstract public void LeaveFromAOI(GM_PlayerEntity e);
        abstract public void OnUpdate(float dt);
        abstract public void OnLateUpdate(float dt);



        public GM_PlayerEntity[] FindEntitiesInArea(GM_PlayerEntity e, float attackR) {
            return null;
        }

        public GM_PlayerEntity FindNearestInArea(GM_PlayerEntity e, float attackR)
        {
            return null;
        }

       
    }

}

