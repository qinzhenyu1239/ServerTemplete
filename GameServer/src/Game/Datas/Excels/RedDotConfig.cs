using System;

namespace Game.Datas.Excels
{
    [Serializable]
    public sealed class RedDotConfig
    {
        public int id;
        public string name;
        public int parentId;
        public int clearType;
        public bool enabled;
        public long tipVersion;
    }
}
