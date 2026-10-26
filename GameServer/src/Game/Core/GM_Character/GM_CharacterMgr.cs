using System.Collections.Generic;
using System.Linq;
using Game.Datas.DBEntities;
using SqlSugar;

namespace Game.Core.GM_Character
{
    /// <summary>Owns shared character-instance operations.</summary>
    public sealed class GM_CharacterMgr
    {
        public static GM_CharacterMgr Instance { get; } = new GM_CharacterMgr();
        private GM_CharacterMgr() { }

        /// <summary>
        /// Checks ownership in the caller's transaction and keeps the matching instances locked until it ends.
        /// Zero represents an empty display slot.
        /// </summary>
        public bool OwnsCharacterInstances(SqlSugarClient db, long playerId, long[] instanceIds)
        {
            long[] ids = instanceIds.Where(id => id != 0).Distinct().ToArray();
            if (ids.Length == 0) return true;
            var parameters = new List<SugarParameter> { new SugarParameter("@owner", playerId) };
            for (int i = 0; i < ids.Length; i++) parameters.Add(new SugarParameter("@instance" + i, ids[i]));
            string placeholders = string.Join(",", Enumerable.Range(0, ids.Length).Select(i => "@instance" + i));
            return db.Ado.SqlQuery<Player_character>(
                "SELECT id FROM player_character WHERE player_id=@owner AND id IN (" + placeholders + ") FOR UPDATE",
                parameters.ToArray()).Count == ids.Length;
        }
    }
}
