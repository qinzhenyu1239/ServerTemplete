using Game.Core.GM_CharacterStarUp;
using Game.Datas.Excels;
using Game.Entries.Modules;
using Game.Core.EntityMgr;
using GameServerTests.Gacha;

namespace GameServerTests.CharacterStarUp;

public sealed class CharacterStarUpConfigurationTests
{
    [Fact]
    public void CurrentCsvConfiguration_LoadsSuccessfully()
    {
        string previous = Environment.CurrentDirectory;
        try
        {
            Environment.CurrentDirectory = AppContext.BaseDirectory;
            CharacterStarUpModule.Instance.Init();
        }
        finally { Environment.CurrentDirectory = previous; }
    }

    [Fact]
    public void BuildDefinitions_CreatesExpectedCosts()
    {
        IReadOnlyDictionary<int, CharacterStarUpDefinition> definitions = CharacterStarUpModule.BuildDefinitions(
            Characters(), Stars(1, 2, 0), Items(), expectedMaxStar: 2);

        CharacterStarUpDefinition definition = Assert.Single(definitions).Value;
        Assert.Equal(2, definition.MaxStar);
        Assert.Equal(30001, definition.PieceItemId);
        Assert.Equal(1, definition.PieceCounts[0]);
        Assert.Equal(2, definition.PieceCounts[1]);
        Assert.Equal(0, definition.PieceCounts[2]);
    }

    [Fact]
    public void BuildDefinitions_RejectsNonContinuousStars()
    {
        List<StarLevel> stars = Stars(1, 2, 0);
        stars.RemoveAt(1);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            CharacterStarUpModule.BuildDefinitions(Characters(), stars, Items(), expectedMaxStar: 2));
        Assert.Contains("continuous", exception.Message);
    }

    [Fact]
    public void BuildDefinitions_RejectsInvalidCosts()
    {
        List<StarLevel> stars = Stars(0, 0);
        Assert.Throws<InvalidOperationException>(() =>
            CharacterStarUpModule.BuildDefinitions(Characters(), stars, Items(), expectedMaxStar: 1));
    }

    [Fact]
    public void BuildDefinitions_RejectsUnknownPieceItem()
    {
        Assert.Throws<InvalidOperationException>(() =>
            CharacterStarUpModule.BuildDefinitions(Characters(), Stars(1, 0),
                new[] { new Item { id = 99999 } }, expectedMaxStar: 1));
    }

    [Fact]
    public void GetCharacterInfo_UsesLoadedOnlineComponentsWithoutDatabaseAccess()
    {
        const long playerId = -991001;
        CharacterStarUpDefinition definition = new()
        {
            CharacterId = 20001,
            PieceItemId = 30001,
            MaxStar = 2,
            PieceCounts = new Dictionary<int, int> { [0] = 1, [1] = 2, [2] = 0 }
        };

        GM_EntityMgr.Instance.Init();
        var entity = GachaMySqlTestEnvironment.AddOnlinePlayer(
            playerId, new Dictionary<int, long> { [30001] = 17 });
        entity.uCharacter.characterIds.Add(20001);
        entity.uCharacter.characterStars[20001] = 1;
        try
        {
            CharacterStarUpInfoResult result =
                GM_CharacterStarUpMgr.Instance.GetCharacterInfo(playerId, definition);

            Assert.Equal(CharacterStarUpStatus.Success, result.Status);
            Assert.Equal(1, result.Star);
            Assert.Equal(17, result.PieceQuantity);
            Assert.Equal(2, result.NextPieceCount);
        }
        finally
        {
            GM_EntityMgr.Instance.RemovePlayer(playerId);
        }
    }

    private static List<Game.Datas.Excels.Character> Characters() => new()
    {
        new Game.Datas.Excels.Character { ID = "1", CardID = "20001", PieceId = "30001" }
    };

    private static List<StarLevel> Stars(params int[] costs) => costs.Select((cost, star) =>
        new StarLevel { ID = (star + 1).ToString(), Star = star.ToString(), PieceCount = cost.ToString() })
        .ToList();

    private static List<Item> Items() => new() { new Item { id = 30001 } };
}
