using System;

using System.Collections;

namespace Game.Datas.Excels {

	[Serializable]
	public class GatewayConfig
	{
		public int ID;
		public string ServerDesic;
		public string IpAddr;
		public int GwConnPort;
		public int Enabled;
	}
}
