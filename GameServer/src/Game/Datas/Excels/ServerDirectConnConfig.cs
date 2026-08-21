using System;

using System.Collections;

namespace Game.Datas.Excels {

	[Serializable]
	public class ServerDirectConnConfig
	{
		public int ID;
		public string ServerDesic;
		public string IpAddr;
		public int TcpPort;
		public int WsPort;
		public int HttpPort;
		public int Enabled;
	}
}
