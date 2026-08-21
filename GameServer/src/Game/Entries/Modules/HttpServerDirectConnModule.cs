using Framework.Core.Utils;
using Game.Datas.Excels;
using LitJson;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;


namespace Game.Entries.Modules
{
    class HttpServerDirectConnModule
    {
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private List<ServerDirectConnConfig> logicServers = null;
        public static HttpServerDirectConnModule Instance = new HttpServerDirectConnModule();

        private string directConnJsonStr = null;

        public void Init() {
            this.logicServers = ExcelUtils.GetConfigDatas<ServerDirectConnConfig>("ServerDirectConnConfig");

            JsonData root = new JsonData();
            JsonData array = new JsonData();
            // array.SetJsonType(JsonType.Object);
            root["servers"] = array;

            for (int i = 0; i < this.logicServers.Count; i++) {
                ServerDirectConnConfig server = this.logicServers[i];
                // JsonData item = .ToObject();
                JsonData data = new JsonData();
                data["ID"] = new JsonData(server.ID);
                data["ServerDesic"] = new JsonData(server.ServerDesic);
                // data["desic"] = new JsonData(zone.desic);
                data["IpAddr"] = new JsonData(server.IpAddr);
                data["TcpPort"] = new JsonData(server.TcpPort);
                data["WsPort"] = new JsonData(server.WsPort);
                data["HttpPort"] = new JsonData(server.HttpPort);

                
                array.Add(data);
            }

            string jsonStr = JsonMapper.ToJson(root);
            Regex reg = new Regex(@"(?i)\\[uU]([0-9a-f]{4})");
            this.directConnJsonStr = reg.Replace(jsonStr, delegate (Match m) { return ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString(); });
            
        }

        public string HandlePullServerDirectConnConfigAction() {
            return this.directConnJsonStr;
        }
    }
}
