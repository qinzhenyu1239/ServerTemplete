using Framework.Core.Net;
using Game.Core.EntityMgr;
using Game.Core.GM_Item;
using Game.Datas.Messages;
using System.Linq;

namespace Game.Entries.Modules
{
    public sealed class ItemModule
    {
        public static ItemModule Instance { get; } = new ItemModule();

        private ItemModule() { }

        public ResGetPlayerItems HandlerGetAll(
            IdSession session, ReqGetPlayerItems request, long playerId)
        {
            if (playerId <= 0 || GM_AccountEntityMgr.Instance.GetPlayerById(playerId) == null)
                return Error(Respones.AccountIsNotLogin, "Player is not logged in.");
            if (request == null)
                return Error(Respones.InvalidParams, "Invalid player items request.");

            ItemInventoryResult result = GM_ItemMgr.Instance.GetAllItems(playerId);
            if (result.Status != ItemInventoryStatus.Success)
                return Error(Respones.SystemErr, "Failed to query player items.");

            return new ResGetPlayerItems
            {
                status = (int)Respones.OK,
                items = result.Items.Select(item => new PlayerItemMessage
                {
                    itemId = item.ItemId,
                    quantity = item.Quantity
                }).ToList()
            };
        }

        private static ResGetPlayerItems Error(Respones status, string message) => new()
        {
            status = (int)status,
            errorMessage = message
        };
    }
}
