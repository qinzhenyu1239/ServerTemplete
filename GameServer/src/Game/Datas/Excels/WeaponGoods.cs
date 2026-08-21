using System;

using System.Collections;

namespace Game.Datas.Excels {

	[Serializable]
	public class WeaponGoods
	{
		public int ID;
		public string name;
		public string EqpmtDesic;
		public float Attack;
		public float Defense;
		public string UpgradeProp;
		public int BuyInPrice;
		public int SellOutPrice;
	}
}
