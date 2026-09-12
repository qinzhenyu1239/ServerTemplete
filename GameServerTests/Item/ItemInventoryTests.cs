using Framework.Core.Serializer;
using Framework.Core.Utils;
using Game;
using Game.Core.Db;
using Game.Core.EntityMgr;
using Game.Core.GM_Item;
using Game.Datas.Messages;
using Game.Entries.Modules;
using GameServerTests.Gacha;
using System.Configuration;
using System.Reflection;

namespace GameServerTests.Inventory;

public sealed class ItemInventoryTests
{
    [Fact]
    public void ItemModule_RejectsUnauthenticatedPlayer()
    {
        ResGetPlayerItems response = ItemModule.Instance.HandlerGetAll(
            null, new ReqGetPlayerItems(), 0);

        Assert.Equal((int)Respones.AccountIsNotLogin, response.status);
        Assert.Empty(response.items);
    }

    [Fact]
    public void GetAllItems_UsesOnlineComponentFiltersNonPositiveAndSorts()
    {
        const long playerId = -992001;
        GM_EntityMgr.Instance.Init();
        GachaMySqlTestEnvironment.AddOnlinePlayer(playerId, new Dictionary<int, long>
        {
            [30003] = 7,
            [30001] = 2,
            [30002] = 0,
            [30000] = -1
        });

        try
        {
            ItemInventoryResult result = GM_ItemMgr.Instance.GetAllItems(playerId);

            Assert.Equal(ItemInventoryStatus.Success, result.Status);
            Assert.Collection(result.Items,
                item =>
                {
                    Assert.Equal(30001, item.ItemId);
                    Assert.Equal(2, item.Quantity);
                },
                item =>
                {
                    Assert.Equal(30003, item.ItemId);
                    Assert.Equal(7, item.Quantity);
                });
        }
        finally
        {
            GM_EntityMgr.Instance.RemovePlayer(playerId);
        }
    }

    [Fact]
    public void GetAllItems_ReturnsFailedWhenOfflineDatabaseReadFails()
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        FieldInfo connectionField = typeof(DBService).GetField("connGameDataStr", flags)
            ?? throw new MissingFieldException(typeof(DBService).FullName, "connGameDataStr");
        object previous = connectionField.GetValue(DBService.Instance);
        GM_EntityMgr.Instance.Init();

        try
        {
            connectionField.SetValue(DBService.Instance, null);
            ItemInventoryResult result = GM_ItemMgr.Instance.GetAllItems(-992002);
            Assert.Equal(ItemInventoryStatus.Failed, result.Status);
            Assert.Empty(result.Items);
        }
        finally
        {
            connectionField.SetValue(DBService.Instance, previous);
        }
    }

    [Fact]
    public void ItemMessages_UseCommands89And90AndPreserveLongQuantityAndRequestId()
    {
        Assert.Equal(89, (int)Cmd.eGetPlayerItemsReq);
        Assert.Equal(90, (int)Cmd.eGetPlayerItemsRes);
        Assert.Equal((short)Game.Module.PLAYER, new ReqGetPlayerItems().GetModule());

        const long quantity = (long)int.MaxValue + 1234L;
        const long requestId = 99887766;
        var response = new ResGetPlayerItems
        {
            status = (int)Respones.OK,
            items = new List<PlayerItemMessage>
            {
                new PlayerItemMessage { itemId = 30001, quantity = quantity }
            }
        };

        byte[] packet = MessagePusher.PackMessage(response, 0, requestId: requestId);
        Assert.Equal(requestId,
            UtilsHelper.ReadLongLE(packet, ProtocolPacketLayout.RequestIdOffset));

        using var stream = new MemoryStream(
            packet, ProtocolPacketLayout.HeaderLength,
            packet.Length - ProtocolPacketLayout.HeaderLength);
        var decoded = ProtoBuf.Serializer.Deserialize<ResGetPlayerItems>(stream);
        Assert.Equal((short)Cmd.eGetPlayerItemsRes, decoded.GetCmd());
        Assert.Equal(quantity, Assert.Single(decoded.items).quantity);
    }

    [DatabaseFact]
    [Trait("Category", "Database")]
    public async Task GetAllItems_OfflineQueryIsPlayerIsolatedAndFiltersNonPositive()
    {
        long playerId = GachaMySqlTestEnvironment.PlayerId(801);
        long otherPlayerId = GachaMySqlTestEnvironment.PlayerId(802);
        string connectionString = GachaMySqlTestEnvironment.ReadConnectionString();
        await using var connection =
            await GachaMySqlTestEnvironment.OpenConnectionAsync(connectionString);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
        await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, otherPlayerId);

        try
        {
            await GachaMySqlTestEnvironment.ExecuteNonQueryAsync(connection,
                $"INSERT INTO player_item(player_id,item_id,quantity) VALUES " +
                $"({playerId},40003,9),({playerId},40001,3),({playerId},40002,0)," +
                $"({otherPlayerId},49999,88);");
            GachaMySqlTestEnvironment.InitializeServerServices(connectionString);

            ItemInventoryResult result = GM_ItemMgr.Instance.GetAllItems(playerId);

            Assert.Equal(ItemInventoryStatus.Success, result.Status);
            Assert.Equal(new[] { 40001, 40003 }, result.Items.Select(item => item.ItemId));
            Assert.Equal(new long[] { 3, 9 }, result.Items.Select(item => item.Quantity));
        }
        finally
        {
            if (!GachaMySqlTestEnvironment.KeepDatabaseData)
            {
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, playerId);
                await GachaMySqlTestEnvironment.ResetPlayerAsync(connection, otherPlayerId);
            }
        }
    }
}
