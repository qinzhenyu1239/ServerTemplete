using System;

using System.Collections;

namespace Game.Datas.Excels {

	[Serializable]
	public class GachaPoolRewardConfig
	{
		public int ID;
		public int poolID;
		public int cardID;
		public int fragmentItemID;
		public int rarity;
		public int weight;
		public int fragmentNum;
	}
}
