using Framework.Core.Serializer;
using Game.Datas.Messages;
using Game.Entries.Modules;

namespace GameServerTests;

public class SessionInvalidationProtocolTests
{
    [Fact]
    public void Protocol1007RoundTripsBase64Signature()
    {
        var request = new ReqInvalidateAccountSession
        {
            accountId = 123,
            sessionVersion = 2,
            sessionId = "session",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            nonce = "nonce",
            signature = Convert.ToBase64String(Enumerable.Range(0, 32).Select(i => (byte)i).ToArray())
        };
        using var stream = new MemoryStream();
        Assert.True(GoogleProtocolCodec.TrySerialize(request, stream, out string writeError), writeError);
        stream.Position = 0;
        Assert.True(GoogleProtocolCodec.TryDeserialize(typeof(ReqInvalidateAccountSession), stream,
            out Message decoded, out string readError), readError);
        var result = Assert.IsType<ReqInvalidateAccountSession>(decoded);
        Assert.Equal(1007, result.GetCmd());
        Assert.Equal(request.accountId, result.accountId);
        Assert.Equal(request.sessionVersion, result.sessionVersion);
        Assert.Equal(request.sessionId, result.sessionId);
        Assert.Equal(request.timestamp, result.timestamp);
        Assert.Equal(request.nonce, result.nonce);
        Assert.Equal(request.signature, result.signature);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    [InlineData("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA==")]
    public void MalformedOrWrongLengthSignatureIsRejected(string signature)
    {
        var request = new ReqInvalidateAccountSession
        {
            accountId = 123,
            sessionVersion = 2,
            sessionId = "session",
            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            nonce = "nonce",
            signature = signature
        };
        Assert.Equal("INVALID", GameAuthModule.Instance.HandlerSessionInvalidation(request));
    }
}
