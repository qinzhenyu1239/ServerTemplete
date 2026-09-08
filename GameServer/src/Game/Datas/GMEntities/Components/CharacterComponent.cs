using Game.Core.GM_CharacterStarUp;
using System.Collections.Generic;

namespace Game.Datas.GMEntities
{
    public struct CharacterComponent
    {
        public HashSet<int> characterIds;
        public Dictionary<int, byte> characterStars;

        public static void Init(GM_PlayerEntity entity) => Reload(entity);
        public static void Reset(GM_PlayerEntity entity) => Reload(entity);

        public static void Reload(GM_PlayerEntity entity)
        {
            GM_CharacterStarUpMgr.Instance.ReloadCharacterComponent(entity);
        }
    }
}
