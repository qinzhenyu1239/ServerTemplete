
using System;

namespace Game.LogicServer
{
    public class SkillModel : Attribute
    {
        public int mainType; // 1000000

        public SkillModel(int mainType)
        {
            this.mainType = mainType;
        }
    }


    public class SkillProcesser : Attribute
    {
        public int subType; // -1为当前类技能默认的处理
        public string funcName;
        public SkillProcesser(string name, int subType)
        {
            this.subType = subType;
            this.funcName = name;
        }
    }

    public class SkillConfig : Attribute
    {
    }

    public class BuffModel : Attribute
    {
        public int mainType;

        public BuffModel(int mainType)
        {
            this.mainType = mainType;
        }
    }


    public class BuffProcesser : Attribute
    {
        public int subType; // -1为当前类buf默认的处理
        public string propName; // 要多不同的属性，进行buff计算，

        public BuffProcesser(string propName, int subType)
        {
            this.subType = subType;
            this.propName = propName;
        }
    }

    public class BuffConfig : Attribute { }

}
