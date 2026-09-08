using Game.Core.GM_Formation;
using System.Collections.Generic;

namespace Game.Datas.GMEntities
{
    public struct FormationComponent
    {
        public bool isLoaded;
        public Dictionary<(int ModuleId, int FormationId), PlayerFormationSnapshot> formations;

        public static void Init(GM_PlayerEntity entity) => Invalidate(entity);
        public static void Reset(GM_PlayerEntity entity) => Invalidate(entity);

        public static void Invalidate(GM_PlayerEntity entity)
        {
            entity.uFormation.isLoaded = false;
            entity.uFormation.formations =
                new Dictionary<(int ModuleId, int FormationId), PlayerFormationSnapshot>();
        }

        public static void EnsureLoaded(GM_PlayerEntity entity)
        {
            if (entity.uFormation.isLoaded) return;
            GM_FormationMgr.Instance.ReloadFormationComponent(entity);
        }
    }
}
