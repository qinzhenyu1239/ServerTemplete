using Framework.Core.Utils;
using Game.Datas.Excels;
using LitJson;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Game.Entries.Modules
{
    public sealed class ServerDirectConnModule
    {
        public static ServerDirectConnModule Instance { get; } = new ServerDirectConnModule();

        private string directConnJson;

        public void Init()
        {
            List<ServerDirectConnConfig> logicServers =
                ExcelUtils.GetConfigDatas<ServerDirectConnConfig>(nameof(ServerDirectConnConfig));

            var root = new JsonData();
            var array = new JsonData();
            root["servers"] = array;

            foreach (ServerDirectConnConfig server in logicServers)
            {
                var data = new JsonData();
                data["ID"] = new JsonData(server.ID);
                data["ServerDesic"] = new JsonData(server.ServerDesic);
                data["IpAddr"] = new JsonData(server.IpAddr);
                data["TcpPort"] = new JsonData(server.TcpPort);
                data["WsPort"] = new JsonData(server.WsPort);
                data["HttpPort"] = new JsonData(server.HttpPort);
                array.Add(data);
            }

            directConnJson = DecodeUnicodeEscapes(JsonMapper.ToJson(root));
        }

        public string HandlerPullServerDirectConnConfig() => directConnJson;

        private static string DecodeUnicodeEscapes(string json) =>
            Regex.Replace(json, @"(?i)\\[uU]([0-9a-f]{4})",
                match => ((char)Convert.ToInt32(match.Groups[1].Value, 16)).ToString());
    }
}
