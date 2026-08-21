using Framework.Core.task;
using Framework.Core.Utils;
using Game.Datas.Excels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game
{
    public class LogicServerFactory
    {
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static LogicServerFactory Instance = new LogicServerFactory();
        private Dictionary<int, BaseLogicServer> logciServerMap = new Dictionary<int, BaseLogicServer>();

        private Dictionary<int, List<BaseLogicServer>> serverTypeIdMap = new Dictionary<int, List<BaseLogicServer>>();


        public void Init() {
            List<LogicServerInst> configs = ExcelUtils.GetConfigDatas<LogicServerInst>(null);
            for (int i = 0; i < configs.Count; i++) {
                LogicServerInst config = configs[i];
                if (config.enabled == 1) {
                    this.CreateLogicServerInst(config);
                }
                
            }
        }

        private void CreateLogicServerInst(LogicServerInst config) {
            BaseLogicServer server = LogicWorkerPool.Instance.AddLogicServerInstance(config.stype, config.ID, config);
            if (server == null) {
                this.logger.Warn($"Create Server Instance Error: {config.ID}");
                return;
            }
            
            this.logciServerMap.Add(config.ID, server);

            List<BaseLogicServer> servers = null;
            if (this.serverTypeIdMap.ContainsKey(config.stype))
            {
                servers = this.serverTypeIdMap[config.stype];
            }
            else {
                servers = new List<BaseLogicServer>();
                this.serverTypeIdMap.Add(config.stype, servers);
            }

            servers.Add(server);
        }

        public bool IsExistInst(int instId) {
            return this.logciServerMap.ContainsKey(instId);
        }

        public BaseLogicServer GetLogciServer(int instId) {
            if (this.logciServerMap.ContainsKey(instId)) {
                return this.logciServerMap[instId];
            }
            return null;
        }

        public int FindLogicServerInst(int typeId, int zid) {
            if (!this.serverTypeIdMap.ContainsKey(typeId)) {
                return -1;
            }
            List<BaseLogicServer> servers = this.serverTypeIdMap[typeId];

            // 查找策略;
            int maxNum = -1;
            int maxIndex = -1;
            for (int i = 0; i < servers.Count; i++) {
                if (servers[i].onlinePlayers > maxNum && servers[i].HasZone(zid)) {
                    maxIndex = i;
                    maxNum = servers[i].onlinePlayers;
                }
            }

            if (maxIndex < 0) {
                return -1;
            }



            return servers[maxIndex].instanceId; 
        }
    }
}
