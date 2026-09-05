using System;

using System.Collections;

namespace Game.Datas.Excels {

	[Serializable]
	public class GachaPoolRewardConfig
	{
		public int ID;
		public int poolID;
		public string remark;
		public string smallPityRarity;
		public string smallPityCount;
		public string bigPityRarity;
		public string bigPityCount;
		public string rarities;
		public string rarityWeights;
		public string ticketItemID;
		public string diamondItemID;
		public string diamondCostPerDraw;
	}
}
