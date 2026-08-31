using System;

using System.Collections;

namespace Game.Datas.Excels {

	[Serializable]
	public class GachaPoolConfig
	{
		public int ID;
		public int poolID;
		public string remark;
		public int smallPityRarity;
		public int smallPityCount;
		public int bigPityRarity;
		public int bigPityCount;
		public string rarities;
		public string rarityWeights;
		public int ticketItemID;
		public int diamondItemID;
		public int diamondCostPerDraw;
	}
}
