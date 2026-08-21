using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.LogicServer.RVOMapEditor {
    [Serializable]
    public class SkillConfig
    {
        public int skillId;

        public float beginTime = 0.0f;
        public float checkTime = 0.0f;
        public float endTime = 0.0f;

        public float attackScale = 1.0f; // 攻击时候加层比例,怎么定，你就和策划的需求来进行设定;

        public int buffTime = 0; // 也可以考虑定点数,  1.5秒---> 1500ms;

        // 根据位置来做线性插值;  ===>策划商量好的;
        public float attackR = 2.0f; // 当我计算伤害的时候，敌人在这个伤害范围内，都会被计算;
                                     // ......

        public string effectPath = null;
    }
}





