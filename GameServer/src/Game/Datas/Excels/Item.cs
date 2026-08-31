using System;

namespace Game.Datas.Excels
{
    [Serializable]
    public class Item
    {
        public int id;
        public string name;
        public string name_cn;
        public int type;
        public string tip;
        public string lvrequirements;
        public int rarity;
        public int maxstack;
        public int showinbag;
        public int isusable;
        public string drop_group_id;
    }
}
