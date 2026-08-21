using System;
using System.Collections;
using System.Collections.Generic;

namespace Game.LogicServer.RVOMapEditor
{
    [Serializable]
    public class FightConfig
    {
        public float moveSpeed = 0.05f;

        public int hp;
        public int baseAttack;
        public int baseDefense;

        public float attackR;
        public float visualR;

        public int useAI = 0;
        // ...
    }
}


