

using Framework.Core.Net;
using Framework.Core.Serializer;
using Framework.Core.task;
using Framework.Core.Utils;
using Game.Datas.Excels;
using Game.Datas.Messages;
using Game.Utils;
using System.Collections.Generic;

namespace Game
{
    public abstract class CommonLogicServer : BaseLogicServer
    {
        // zone set
        protected Dictionary<int, ZoneConfig> zoneSet = null;

        public virtual void OnStart() {
            this.zoneSet = new Dictionary<int, ZoneConfig>();

            LogicServerInst config = this.instConfig as LogicServerInst;
            if (config == null) {
                return;
            }
            List<int> zones = GameUtils.ParseStringWithIntValues(config.zones);

            // 为每个zid创建一个等待列表List;
            for (int i = 0; i < zones.Count; i++)
            {
                int zid = zones[i];  // zid ===> ZoneConfig配置;

                ZoneConfig zConfig = (ZoneConfig)ExcelUtils.GetConfigData<ZoneConfig>(zid.ToString());
                this.zoneSet.Add(zid, zConfig);
            }
        }

        public override bool HasZone(int zid)
        {
            if (this.zoneSet == null || !this.zoneSet.ContainsKey(zid))
            {
                return false;
            }

            return true;
        }

        [RequestMapping]
        public object DoReqTestEchoLogicServer(IdSession s, ReqTestLogicCmdEcho req, long playerId)
        {
            ResTestLogicCmdEcho res = new ResTestLogicCmdEcho();
            res.content = req.content;
            return res;
        }

        [RequestMapping]
        public object DoReqExitLogicServer(IdSession s, ReqExitLogicServer req, long playerId)
        {
            int status = this.ExitLogicServer(playerId, req.quitReason);
            ResExitLogicServer res = null;
            if (req.quitReason != (int)QuitReason.DisconnectQuit) {
                res = new ResExitLogicServer();
            }
            if (req.quitReason != (int)QuitReason.DisconnectQuit) {
                res.status = status;
            }
            if (req.quitReason == (int)QuitReason.DisconnectQuit) { 
                if (status == (int)Respones.UserIsPlaying) { // 正在游戏中却断线了，
                }
                s.logicServerId = -1;
            }
            if (status != (int)Respones.OK) {
                return res;
            }
            s.logicServerId = -1;
            return res;
        }
    }
}
