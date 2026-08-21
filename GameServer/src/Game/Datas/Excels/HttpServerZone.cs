using System;

using System.Collections;

namespace Game.Datas.Excels {

	[Serializable]
	public class HttpServerZone
	{
		public int ID;
		public string GameZone;
		public string desic;
		public string ServerIp;
		public int TcpPort;
		public int WsPort;
		public int HttpPort;
		public string HttpWebsite;
		public int useDirectConn;
	}
}
