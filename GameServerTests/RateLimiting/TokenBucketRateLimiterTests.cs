using Framework.Core.Net;
using Framework.Core.RateLimiting;
using System.Diagnostics;

namespace GameServerTests.RateLimiting;

public sealed class TokenBucketRateLimiterTests
{
    [Fact]
    public void TryAcquire_RejectsAfterBurstCapacityIsConsumed()
    {
        var limiter = new TokenBucketRateLimiter<long>(
            2, 2, TimeSpan.FromMinutes(1));

        Assert.True(limiter.TryAcquire(1, 0));
        Assert.True(limiter.TryAcquire(1, 0));
        Assert.False(limiter.TryAcquire(1, 0));
    }

    [Fact]
    public void TryAcquire_RefillsAtConfiguredSustainedRate()
    {
        var limiter = new TokenBucketRateLimiter<long>(
            2, 2, TimeSpan.FromMinutes(1));
        Assert.True(limiter.TryAcquire(1, 0));
        Assert.True(limiter.TryAcquire(1, 0));

        long halfSecond = Stopwatch.Frequency / 2;

        Assert.True(limiter.TryAcquire(1, halfSecond));
        Assert.False(limiter.TryAcquire(1, halfSecond));
    }

    [Fact]
    public void TryAcquire_MaintainsIndependentBucketsPerKey()
    {
        var limiter = new TokenBucketRateLimiter<string>(
            1, 1, TimeSpan.FromMinutes(1), StringComparer.Ordinal);

        Assert.True(limiter.TryAcquire("first", 0));
        Assert.False(limiter.TryAcquire("first", 0));
        Assert.True(limiter.TryAcquire("second", 0));
    }
}

public sealed class RequestRateLimiterTests
{
    [Fact]
    public void IsExempt_OnlyExcludesSceneTraffic()
    {
        Assert.True(MessageDispatcher.IsRateLimitExempt((short)Game.Module.SCENE));
        Assert.False(MessageDispatcher.IsRateLimitExempt((short)Game.Module.ACTIVITY));
        Assert.False(MessageDispatcher.IsRateLimitExempt((short)Game.Module.PLAYER));
    }

    [Theory]
    [InlineData("/ReqGuestLogin")]
    [InlineData("/ReqRegisterUser")]
    [InlineData("/ReqUserLogin")]
    [InlineData("/ReqGuestUpgrade")]
    public void IsCredentialRoute_RecognizesCredentialEndpoints(string uri)
    {
        Assert.Equal(HttpRequestRateLimitKind.Credential, HttpRouteDispatcher.GetRateLimitKind(uri));
    }


    [Fact]
    public void GetRateLimitKind_RecognizesTicketAndDefaultEndpoints()
    {
        Assert.Equal(
            HttpRequestRateLimitKind.Ticket,
            HttpRouteDispatcher.GetRateLimitKind("/ReqIssueGameTicket"));
        Assert.Equal(
            HttpRequestRateLimitKind.Default,
            HttpRouteDispatcher.GetRateLimitKind("/health"));
    }
}
