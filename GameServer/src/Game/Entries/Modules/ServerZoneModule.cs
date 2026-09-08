using Framework.Core.Utils;
using Game.Datas.Excels;
using LitJson;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Game.Entries.Modules
{
    public sealed class ServerZoneModule
    {
        public static ServerZoneModule Instance { get; } = new ServerZoneModule();

        private string serverZoneJson;

        public void Init()
        {
            List<HttpServerZone> serverZones =
                ExcelUtils.GetConfigDatas<HttpServerZone>(nameof(HttpServerZone));

            var root = new JsonData();
            var array = new JsonData();
            root["ServerZones"] = array;

            foreach (HttpServerZone zone in serverZones)
            {
                var data = new JsonData();
                data["ID"] = new JsonData(zone.ID);
                data["GameZone"] = new JsonData(zone.GameZone);
                data["ServerIp"] = new JsonData(zone.ServerIp);
                data["TcpPort"] = new JsonData(zone.TcpPort);
                data["WsPort"] = new JsonData(zone.WsPort);
                data["HttpPort"] = new JsonData(zone.HttpPort);
                data["HttpWebsite"] = new JsonData(zone.HttpWebsite);
                data["useDirectConn"] = new JsonData(zone.useDirectConn);
                array.Add(data);
            }

            serverZoneJson = DecodeUnicodeEscapes(JsonMapper.ToJson(root));
        }

        public string HandlerPullServerZone() => serverZoneJson;

        private static string DecodeUnicodeEscapes(string json) =>
            Regex.Replace(json, @"(?i)\\[uU]([0-9a-f]{4})",
                match => ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString());
    }
}
