using Game.Core.GM_Formation;

namespace GameServerTests.Formation;

public sealed class FormationRulesTests
{
    [Theory]
    [InlineData(0, 9, 1, 6)]
    [InlineData(1, 9, 1, 6)]
    [InlineData(2, 12, 12, 12)]
    public void Rules_AreAuthoritative(int moduleId, int slots, int min, int max)
    {
        Assert.True(FormationRules.TryGet(moduleId, out FormationModuleRule rule));
        Assert.Equal(slots, rule.SlotCount);
        Assert.Equal(min, rule.MinDeployCount);
        Assert.Equal(max, rule.MaxDeployCount);
    }

    [Fact]
    public void Validate_AcceptsModuleZeroPartialAndModuleTwoFull()
    {
        Assert.True(GM_FormationMgr.Validate(0, 4, 0, Values(6, 0)));
        Assert.True(GM_FormationMgr.Validate(2, 0, 0, Values(12, 0)));
    }

    [Fact]
    public void Validate_RejectsInvalidCountsSlotsAndDuplicates()
    {
        Assert.False(GM_FormationMgr.Validate(0, 0, 0, Array.Empty<FormationSlotValue>()));
        Assert.False(GM_FormationMgr.Validate(0, 0, 0, Values(7, 0)));
        Assert.False(GM_FormationMgr.Validate(2, 0, 0, Values(11, 0)));
        Assert.False(GM_FormationMgr.Validate(0, 5, 0, Values(1, 0)));
        Assert.False(GM_FormationMgr.Validate(0, 0, -1, Values(1, 0)));
        Assert.False(GM_FormationMgr.Validate(0, 0, 0, new[]
        {
            new FormationSlotValue { SlotIndex = 0, CharacterId = 1001 },
            new FormationSlotValue { SlotIndex = 0, CharacterId = 1002 }
        }));
        Assert.False(GM_FormationMgr.Validate(0, 0, 0, new[]
        {
            new FormationSlotValue { SlotIndex = 0, CharacterId = 1001 },
            new FormationSlotValue { SlotIndex = 1, CharacterId = 1001 }
        }));
        Assert.False(GM_FormationMgr.Validate(0, 0, 0, new[]
        {
            new FormationSlotValue { SlotIndex = 9, CharacterId = 1001 }
        }));
    }

    private static IReadOnlyCollection<FormationSlotValue> Values(int count, int firstSlot) =>
        Enumerable.Range(0, count).Select(index => new FormationSlotValue
        {
            SlotIndex = firstSlot + index,
            CharacterId = 1000 + index
        }).ToList();
}
