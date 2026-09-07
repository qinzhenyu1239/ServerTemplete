using Game.Core.Idempotency;
using Game.Core.GM_Gacha;

namespace GameServerTests.Idempotency;

public sealed class EconomicOperationUtilsTests
{
    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef", true)]
    [InlineData("01234567-89ab-cdef-0123-456789abcdef", false)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF", false)]
    [InlineData("00000000000000000000000000000000", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void OperationIdValidation_RequiresLowercaseUuidN(string value, bool expected) =>
        Assert.Equal(expected, OperationIdValidator.IsValid(value));

    [Fact]
    public void RequestHash_IsDeterministicAndParameterSensitive()
    {
        string first = OperationRequestHasher.Compute("gacha", 1, 10);
        Assert.Equal(first, OperationRequestHasher.Compute("gacha", 1, 10));
        Assert.NotEqual(first, OperationRequestHasher.Compute("gacha", 1, 1));
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void ResponseSnapshot_RestoresGachaRewardList()
    {
        var source = new GachaDrawResult
        {
            OperationId = "0123456789abcdef0123456789abcdef",
            Status = GachaOperationStatus.Success,
            BatchId = 42,
            Rewards = new[]
            {
                new GachaDrawRewardResult { DrawIndex = 1, CharacterId = 10001, Rarity = 4 }
            }
        };
        string json = EconomicOperationPersistence.Serialize(source);
        GachaDrawResult restored =
            EconomicOperationPersistence.Deserialize<GachaDrawResult>(json);
        Assert.Equal(source.OperationId, restored.OperationId);
        Assert.Equal(42, restored.BatchId);
        Assert.Equal(10001, Assert.Single(restored.Rewards).CharacterId);
    }
}
