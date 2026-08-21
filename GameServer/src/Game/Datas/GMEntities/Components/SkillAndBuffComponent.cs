using Game.Datas.GMEntities;
using Game.LogicServer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Game.Datas.GMEntities
{
    public struct SkillAndBuffComponent {

        public object worldOrRoom;
        public SkillTimeLine skillTimeLine;
        public BuffTimeLine buffTimeLine;

        
        public static void Init(GM_PlayerEntity entity) {
            entity.uSkillAndBuff.worldOrRoom = null;
            entity.uSkillAndBuff.buffTimeLine.Init();
            entity.uSkillAndBuff.skillTimeLine.Init();
        }
    }
}
