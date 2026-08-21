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
    class HttpServerZoneModule
    {
        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        private List<HttpServerZone> serverZones = null;
        public static HttpServerZoneModule Instance = new HttpServerZoneModule();

        private string serverZoneJsonStr = null;
        public void Init() {
            this.serverZones = ExcelUtils.GetConfigDatas<HttpServerZone>("HttpServerZone");

            JsonData root = new JsonData();
            JsonData array = new JsonData();
            root["ServerZones"] = array;

            for (int i = 0; i < this.serverZones.Count; i++) {
                HttpServerZone zone = this.serverZones[i];
                // JsonData item = .ToObject();
                JsonData data = new JsonData();
                data["ID"] = new JsonData(zone.ID);
                data["GameZone"] = new JsonData(zone.GameZone);
                // data["desic"] = new JsonData(zone.desic);
                data["ServerIp"] = new JsonData(zone.ServerIp);
                data["TcpPort"] = new JsonData(zone.TcpPort);
                data["WsPort"] = new JsonData(zone.WsPort);
                data["HttpPort"] = new JsonData(zone.HttpPort);
                data["HttpWebsite"] = new JsonData(zone.HttpWebsite);
                data["useDirectConn"] = new JsonData(zone.useDirectConn);
                array.Add(data);
            }

            string jsonStr = JsonMapper.ToJson(root);
            Regex reg = new Regex(@"(?i)\\[uU]([0-9a-f]{4})");
            this.serverZoneJsonStr = reg.Replace(jsonStr, delegate (Match m) { return ((char)Convert.ToInt32(m.Groups[1].Value, 16)).ToString(); });
            
        }

        public string HandlePullServerZoneAction() {
            return this.serverZoneJsonStr;
        }
    }
}
