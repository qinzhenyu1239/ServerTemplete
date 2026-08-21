using Framework.Core.Serializer;

using Game.Core.EntityMgr;
using Game.Datas.DBEntities;
using Game.Datas.GMEntities;
using Game.Datas.Messages;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Npgsql.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.LogicServer
{
    class AOIBlock { // 每个格子，就是一个AOIBlock;
        public Dictionary<long, GM_PlayerEntity> players;

        // NPC,
        // public Dictionary<long, GM_NPCEntity> npcs;
        // 敌人
        // public Dictionary<long, GM_MonestEntity> monests;
        // 物品
        // public Dictionary<long, GM_GoodEntity> goods;
    }

    public struct AOIPoint {
        public int blockX;
        public int blockZ;

        public AOIPoint(int x, int z) {
            this.blockX = x;
            this.blockZ = z;
        }
    }

    public class NineGridAOIMgr : AOIMgr
    {
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private int viewSize = 128 * 3; // 可以去做这个测试 
        private int blockSize;

        private int horBlockCount;
        private int verBlockCount;

        private AOIBlock[,] aoiBlocks = null;

        public override void Init(IMapWrapper mapWrapper, int viewSize = 128 * 3) {
            base.Init(mapWrapper, viewSize);

            // 九宫格AOI的初始化;
            this.viewSize = viewSize;
            this.blockSize = this.viewSize / 3;

            // xoz平面, 左下角为原点, Transform坐标，换成Block会快;
            this.horBlockCount = (mapWrapper.MapWidth() + this.blockSize - 1) / this.blockSize;
            this.verBlockCount = (mapWrapper.MapHeight() + this.blockSize - 1) / this.blockSize;

            this.aoiBlocks = new AOIBlock[this.horBlockCount, this.verBlockCount];

            for (int i = 0; i < this.horBlockCount; i++) {
                for (int j = 0; j < this.verBlockCount; j++) {
                    this.aoiBlocks[i, j] = new AOIBlock();
                    this.aoiBlocks[i, j].players = new Dictionary<long, GM_PlayerEntity>();
                }
            }
        }

        private void BroadMsgInBlock(AOIBlock block, Message s, long ignorePlayerId = -1, List<GM_PlayerEntity> toPlayers = null) {
            foreach (var playerId in block.players.Keys)
            {
                if (playerId == ignorePlayerId) {
                    continue;
                }

                this.SendMsg(block.players[playerId], s);
                if (toPlayers != null) {
                    toPlayers.Add(block.players[playerId]);
                }
            }
        }

        public override void BroadMsgToAOI(BaseEntity center, Message s, long ignorePlayerId = -1, List<GM_PlayerEntity> toPlayers = null)
        {
            if (center.uAoi.blockX < 0 || center.uAoi.blockZ < 0)
            {
                this.logger.Warn("Invalid BlockX or BlockZ !");
                return;
            }

            // 中心
            int blockX = center.uAoi.blockX;
            int blockZ = center.uAoi.blockZ;

            AOIBlock block = this.aoiBlocks[blockX, blockZ];
            this.BroadMsgInBlock(block, s, ignorePlayerId, toPlayers);
            // end
            
            // 左边
            if (blockX - 1 >= 0) {
                block = this.aoiBlocks[blockX - 1, blockZ];
                this.BroadMsgInBlock(block, s, ignorePlayerId, toPlayers);
            }
            // end

            // 右边
            if (blockX + 1 < this.horBlockCount) {
                block = this.aoiBlocks[blockX + 1, blockZ];
                this.BroadMsgInBlock(block, s, ignorePlayerId, toPlayers);
            }
            // end

            // 上边
            if (blockZ + 1 < this.verBlockCount) {
                block = this.aoiBlocks[blockX, blockZ + 1];
                this.BroadMsgInBlock(block, s, ignorePlayerId, toPlayers);
            }
            // end


            // 下边
            if (blockZ - 1 >= 0) {
                block = this.aoiBlocks[blockX, blockZ - 1];
                this.BroadMsgInBlock(block, s, ignorePlayerId, toPlayers);
            }
            // end

            // 左上
            if (blockX - 1 >= 0 && blockZ + 1 < this.verBlockCount) {
                block = this.aoiBlocks[blockX - 1, blockZ + 1];
                this.BroadMsgInBlock(block, s, ignorePlayerId, toPlayers);
            }
            // end

            // 左下
            if (blockX - 1 >= 0 && blockZ - 1 >= 0) {
                block = this.aoiBlocks[blockX - 1, blockZ - 1];
                this.BroadMsgInBlock(block, s, ignorePlayerId, toPlayers);
            }
            // end

            // 右上
            if (blockX + 1 < this.horBlockCount && blockZ + 1 < this.verBlockCount) {
                block = this.aoiBlocks[blockX + 1, blockZ + 1];
                this.BroadMsgInBlock(block, s, ignorePlayerId, toPlayers);
            }
            // end

            // 右下
            if (blockX + 1 < this.horBlockCount && blockZ - 1 >= 0) {
                block = this.aoiBlocks[blockX + 1, blockZ - 1];
                this.BroadMsgInBlock(block, s, ignorePlayerId, toPlayers);
            }
            // end

        }

        private void BroadcastSelfRunStateToPlayers(GM_PlayerEntity e, List<GM_PlayerEntity> toPlayers) {
            ResNavToDst res = new ResNavToDst();
            res.worldId = e.uWorldGame.worldId;


            res.x = e.uAoi.xDst;
            res.y = e.uAoi.yDst;
            res.z = e.uAoi.zDst;

            res.speed = e.uProps.speed;

            for (int i = 0; i < toPlayers.Count; i++) {
                this.SendMsg(toPlayers[i], res);
            }
        }

        private void SendOtherRuningPlayersToSelf(GM_PlayerEntity e, List<GM_PlayerEntity> toPlayers) {
            for (int i = 0; i < toPlayers.Count; i++) {
                if (toPlayers[i] == e || toPlayers[i].uStatus.status != (int)CharactorStatus.Run) {
                    continue;
                }

                ResNavToDst res = new ResNavToDst();
                res.worldId = toPlayers[i].uWorldGame.worldId;


                res.x = toPlayers[i].uAoi.xDst;
                res.y = toPlayers[i].uAoi.yDst;
                res.z = toPlayers[i].uAoi.zDst;

                res.speed = e.uProps.speed;

                this.SendMsg(e, res);
            }
        }

        public override void EnterToAOI(GM_PlayerEntity e)
        {
            long playerId = e.uPlayer.playerInfo.id;
            // 根据你的位置，算出来你是再哪个Block
            int blockX = ((int)(e.uTransform.pos.x)) / this.blockSize;
            int blockZ = ((int)(e.uTransform.pos.z)) / this.blockSize;

            e.uAoi.blockX = blockX;
            e.uAoi.blockZ = blockZ;

            this.aoiBlocks[blockX, blockZ].players.Add(playerId, e);
            // end

            List<GM_PlayerEntity> toPlayers = new List<GM_PlayerEntity>();
            // 告诉别人的客户端，你出现在他们的视野了;
            ResEnterAOI res = new ResEnterAOI();
            List<ArrivedCharactor> arrivedCharactors = new List<ArrivedCharactor>();
            ArrivedCharactor ch = this.GetArrivedCharactor(e);
            arrivedCharactors.Add(ch);
            res.charactors = arrivedCharactors.ToArray();
            this.BroadMsgToAOI(e, res, playerId, toPlayers);

            // 移动
            if (e.uStatus.status == (int)(CharactorStatus.Run)) {
                this.BroadcastSelfRunStateToPlayers(e, toPlayers);
            }
            // end



            // 还要把别人 + 你自己，发给你自己这个客户端,告诉客户端，这些人出现在你视野;
            toPlayers.Add(e);
            arrivedCharactors = new List<ArrivedCharactor>();
            res = new ResEnterAOI();

            for (int i = 0; i < toPlayers.Count; i ++) {
                ch = this.GetArrivedCharactor(toPlayers[i]);
                arrivedCharactors.Add(ch);
            }

            res.charactors = arrivedCharactors.ToArray();
            this.SendMsg(e, res);

            this.SendOtherRuningPlayersToSelf(e, toPlayers);
            // end
        }

        public override void LeaveFromAOI(GM_PlayerEntity e)
        {
            long playerId = e.uPlayer.playerInfo.id;
            int blockX = e.uAoi.blockX;
            int blockZ = e.uAoi.blockZ;

            if (blockX < 0 || blockZ < 0) {
                return;
            }

            ResLeaveAOI res = new ResLeaveAOI();
            res.leavePlayers = new int[1]; // 当我们一个块进入视野，一个块在视野消失,还是用数组;
            res.leavePlayers[0] = e.uWorldGame.worldId;

            this.BroadMsgToAOI(e, res, playerId); // 是否排除自己，都可以，看你的需求;

            // 把自己从Block删除掉
            AOIBlock block = this.aoiBlocks[blockX, blockZ];
            block.players.Remove(playerId);
            // end

            // 离开AOI;
            e.uAoi.blockX = -1;
            e.uAoi.blockZ = -1;
            // end 

        }

        private void GetAOIPoints(int blockX, int blockZ, List<AOIPoint> points)
        {
            points.Add(new AOIPoint(blockX, blockZ));
            // 左
            if (blockX - 1 >= 0)
            {
                points.Add(new AOIPoint(blockX - 1, blockZ));
            }
            // end

            // 右
            if (blockX + 1 < this.horBlockCount)
            {
                points.Add(new AOIPoint(blockX + 1, blockZ));
            }
            // end

            // 上
            if (blockZ + 1 < this.verBlockCount)
            {
                points.Add(new AOIPoint(blockX, blockZ + 1));
            }
            // end

            // 下
            if (blockZ - 1 >= 0)
            {
                points.Add(new AOIPoint(blockX, blockZ - 1));
            }
            // end

            // 左上
            if (blockX - 1 >= 0 && blockZ + 1 < this.verBlockCount)
            {
                points.Add(new AOIPoint(blockX - 1, blockZ + 1));
            }
            // end

            // 左下
            if (blockX - 1 >= 0 && blockZ - 1 >= 0)
            {
                points.Add(new AOIPoint(blockX - 1, blockZ - 1));
            }
            // end

            // 右上
            if (blockX + 1 < this.horBlockCount && blockZ + 1 < this.verBlockCount)
            {
                points.Add(new AOIPoint(blockX + 1, blockZ + 1));
            }
            // end

            // 右下
            if (blockX + 1 < this.horBlockCount && blockZ - 1 >= 0)
            {
                points.Add(new AOIPoint(blockX + 1, blockZ - 1));
            }
            // end
        }

        private bool isInList(List<AOIPoint> list, AOIPoint value)
        {

            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].blockX == value.blockX && list[i].blockZ == value.blockZ)
                {
                    return true;
                }
            }

            return false;
        }

        private void OnGridChangedUpdate(GM_PlayerEntity e) {
            long playerId = e.uPlayer.playerInfo.id;
            // 根据你的位置，算出来你是再哪个Block
            int blockX = ((int)(e.uTransform.pos.x)) / this.blockSize;
            int blockZ = ((int)(e.uTransform.pos.z)) / this.blockSize;

            if (blockX == e.uAoi.blockX && blockZ == e.uAoi.blockZ) { // 九宫格无变化
                return;
            }

            List<AOIPoint> oldGrids = new List<AOIPoint>();
            this.GetAOIPoints(e.uAoi.blockX, e.uAoi.blockZ, oldGrids);
            List<AOIPoint> newGrids = new List<AOIPoint>();
            this.GetAOIPoints(blockX, blockZ, newGrids);

            // 玩家发生了格子变化;
            // step1: 找出在新九宫格的格子但是不在旧九宫格里的格子;
            // 通知这些格子，玩家来了;
            ResEnterAOI res = new ResEnterAOI();
            List<ArrivedCharactor> arrivedCharactors = new List<ArrivedCharactor>();
            ArrivedCharactor ch = this.GetArrivedCharactor(e);
            arrivedCharactors.Add(ch);
            res.charactors = arrivedCharactors.ToArray();
            // end

            List<GM_PlayerEntity> toPlayers = new List<GM_PlayerEntity>();
            for (int i = 0; i < newGrids.Count; i++) {
                if (!this.isInList(oldGrids, newGrids[i])) {
                    AOIBlock block = this.aoiBlocks[newGrids[i].blockX, newGrids[i].blockZ];
                    this.BroadMsgInBlock(block, res, playerId, toPlayers);
                }
            }

            // 通知玩家，你有新的人进来了
            if (toPlayers.Count > 0) {
                this.BroadcastSelfRunStateToPlayers(e, toPlayers);

                arrivedCharactors = new List<ArrivedCharactor>();
                res = new ResEnterAOI();

                for (int i = 0; i < toPlayers.Count; i++)
                {
                    ch = this.GetArrivedCharactor(toPlayers[i]);
                    arrivedCharactors.Add(ch);
                }
                res.charactors = arrivedCharactors.ToArray();
                this.SendMsg(e, res);

                this.SendOtherRuningPlayersToSelf(e, toPlayers);
            }

            // end

            // step2: 找出不在新九宫格里的格子，但是在旧九宫格里的格子;
            ResLeaveAOI leaveRes = new ResLeaveAOI();
            leaveRes.leavePlayers = new int[1]; // 当我们一个块进入视野，一个块在视野消失,还是用数组;
            leaveRes.leavePlayers[0] = e.uWorldGame.worldId;
            toPlayers.Clear();

            for (int i = 0; i < oldGrids.Count; i++)
            {
                if (!this.isInList(newGrids, oldGrids[i]))
                {
                    AOIBlock block = this.aoiBlocks[oldGrids[i].blockX, oldGrids[i].blockZ];
                    // 广播告诉这些块里面的玩家，你离开视野了;
                    this.BroadMsgInBlock(block, leaveRes, playerId, toPlayers);
                }
            }
            // end

            // 再来通知玩家对应的客户端，toPlayers 这些玩家要移出你的视野
            if (toPlayers.Count > 0) {
                leaveRes = new ResLeaveAOI();
                leaveRes.leavePlayers = new int[toPlayers.Count];

                for (int i = 0; i < toPlayers.Count; i++) {
                    leaveRes.leavePlayers[i] = toPlayers[i].uWorldGame.worldId;
                }

                this.SendMsg(e, leaveRes);
            }

            // 更新格子
            AOIBlock oldBlock = this.aoiBlocks[e.uAoi.blockX, e.uAoi.blockZ];
            oldBlock.players.Remove(playerId);

            AOIBlock newBlock = this.aoiBlocks[blockX, blockZ];
            newBlock.players.Add(playerId, e);

            e.uAoi.blockX = blockX;
            e.uAoi.blockZ = blockZ;
        }

        private void OnNavSystemUpdate(float dt)
        {
            foreach (var block in this.aoiBlocks) {
                foreach (var e in block.players.Values)
                {
                    if (e.uStatus.status == (int)CharactorStatus.Run) {
                        this.mapWrapper.OnNavUpdate(this, e, dt);

                        // 做好格子的变化处理;
                        this.OnGridChangedUpdate(e);
                        // end
                    }
                }
            }
            
        }

        private void OnNavSystemLateUpdate(float dt)
        {
            foreach (var block in this.aoiBlocks)
            {
                foreach (var e in block.players.Values)
                {
                    if (e.uStatus.status == (int)CharactorStatus.Run)
                    {
                        this.mapWrapper.OnNavLateUpdate(this, e, dt);
                    }
                }
            }

        }


        private void OnSkillAndBuffUpdate(float dt)
        {
            foreach (var block in this.aoiBlocks) {
                foreach (var e in block.players.Values) {
                    SkillAndBuffSystem.Update(this, e, dt);
                }
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
            // NavSystem LateUpdate迭代
            this.OnNavSystemLateUpdate(dt);
            // end
        }
    }
}
