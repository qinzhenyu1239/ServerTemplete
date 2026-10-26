using Framework.Core.Serializer;
using Game;
using Game.Core.GM_RedDot;
using Game.Datas.Excels;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace GameServerTests.RedDot;

public sealed class RedDotTests
{
    private static RedDotConfig Node(int id, int parent = 0, int type = 0, bool enabled = true, long version = 0) =>
        new() { id = id, name = "node" + id, parentId = parent, clearType = type, enabled = enabled, tipVersion = version };

    [Fact]
    public void RejectsDuplicateMissingParentCycleAndBusinessParent()
    {
        Assert.Throws<InvalidOperationException>(() => new RedDotCatalog(new[] { Node(1), Node(1) }));
        Assert.Throws<InvalidOperationException>(() => new RedDotCatalog(new[] { Node(1, 2) }));
        Assert.Throws<InvalidOperationException>(() => new RedDotCatalog(new[] { Node(1, 2), Node(2, 1) }));
        Assert.Throws<InvalidOperationException>(() => new RedDotCatalog(new[] { Node(1, type: 1), Node(2, 1) }));
    }
    [Fact]
    public void DisabledAncestorDisablesEntireSubtree()
    {
        var c = new RedDotCatalog(new[] { Node(1, enabled: false), Node(2, 1), Node(3, 2, 1) });
        Assert.False(c.IsEnabled(1)); Assert.False(c.IsEnabled(3)); Assert.False(c.IsEnabled(9));
    }
    [Fact]
    public void VersionIsStableAcrossRowOrderAndIncludesDisabledFields()
    {
        var a = Node(1); var b = Node(2, 1, 2, false, 1);
        string v = new RedDotCatalog(new[] { a, b }).Version;
        Assert.Equal(v, new RedDotCatalog(new[] { b, a }).Version);
        b.tipVersion = 2;
        Assert.NotEqual(v, new RedDotCatalog(new[] { a, b }).Version);
    }
    [Fact]
    public void ViewOnlyAcceptsEnabledViewedLeafAndNonFutureVersion()
    {
        var c = new RedDotCatalog(new[] { Node(1), Node(2, 1, 2, true, 5), Node(3, 1, 1) });
        Assert.True(c.CanView(2, 3)); Assert.True(c.CanView(2, 5));
        Assert.False(c.CanView(2, 6)); Assert.False(c.CanView(2, 0));
        Assert.False(c.CanView(1, 1)); Assert.False(c.CanView(3, 1));
    }
    [Fact]
    public void ModuleRejectsMissingSessionBeforeDatabaseAccess()
    {
        Assert.Equal((int)Respones.AccountIsNotLogin,
            RedDotModule.Instance.Initialize(null, new ReqRedDotInit(), 0).status);
        Assert.Equal((int)Respones.AccountIsNotLogin,
            RedDotModule.Instance.MarkViewed(null, new ReqRedDotView(), 0).status);
    }
    [Fact]
    public void GoogleCodecRoundTripsAllConfigFieldsLongVersionsAndFalseDeltas()
    {
        var message = new ResRedDotInit { status = 1, configVersion = "hash", stateVersion = 5_000_000_001,
            nodes = new() { new() { id = 2, name = "邮件", parentId = 1, clearType = 2, enabled = false, tipVersion = 7_000_000_001 } },
            states = new() { new() { nodeId = 2, isActive = false } } };
        using var stream = new MemoryStream();
        Assert.True(GoogleProtocolCodec.TrySerialize(message, stream, out var error), error);
        stream.Position = 0;
        Assert.True(GoogleProtocolCodec.TryDeserialize(typeof(ResRedDotInit), stream, out var result, out error), error);
        var decoded = Assert.IsType<ResRedDotInit>(result);
        Assert.Equal(message.stateVersion, decoded.stateVersion);
        Assert.Equal(message.nodes[0].tipVersion, Assert.Single(decoded.nodes).tipVersion);
        Assert.False(decoded.nodes[0].enabled);
        Assert.False(Assert.Single(decoded.states).isActive);
        Assert.Equal(95, (int)Cmd.eRedDotInitReq);
        Assert.Equal(99, (int)Cmd.eRedDotChanged);
    }
}
