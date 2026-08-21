using Game.Core.GM_Task;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;

namespace Game.Datas.GMEntities
{
    public struct PropsComponent
    {
        public float speed; // 玩家的移动速度，

        public int hp;
        public int attack; // 基本的攻击力，不包含buff叠加
        public int defense; // 基本的防御力，不包含buff叠加

        public static void Init(GM_PlayerEntity entity /*暂时在代码写死，后续Config传入*/)
        {
            entity.uProps.speed = 5.0f;
            entity.uProps.hp = 500;
            entity.uProps.attack = 75;
            entity.uProps.defense = 50;
        }
    }
}
