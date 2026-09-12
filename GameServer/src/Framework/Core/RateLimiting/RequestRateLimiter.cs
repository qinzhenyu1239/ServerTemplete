using DotNetty.Transport.Channels;
using Framework.Core.Net;
using System;
using System.Net;

namespace Framework.Core.RateLimiting
{
    internal sealed class RequestRateLimiter
    {
        internal static readonly RequestRateLimiter Instance = new();
        private readonly TokenBucketRateLimiter<long> globalPrincipalLimiter = new(60, 20, TimeSpan.FromMinutes(10));
        private readonly TokenBucketRateLimiter<string> globalIpLimiter = new(200, 50, TimeSpan.FromMinutes(10), StringComparer.Ordinal);
        private readonly TokenBucketRateLimiter<string> authenticationIpLimiter = new(10, 1, TimeSpan.FromMinutes(10), StringComparer.Ordinal);
        private RequestRateLimiter() { }

        internal bool TryAcquireDirect(IdSession session, bool exempt, bool authentication)
        {
            if (exempt) return true;
            string ip = GetIp(session?.client);
            if (authentication) return !string.IsNullOrWhiteSpace(ip) && authenticationIpLimiter.TryAcquire(ip);
            if (!string.IsNullOrWhiteSpace(ip) && !globalIpLimiter.TryAcquire(ip)) return false;
            long principalId = session?.playerId > 0 ? session.playerId : session?.accountId ?? 0;
            return principalId > 0 && globalPrincipalLimiter.TryAcquire(principalId);
        }

        internal bool TryAcquireGateway(long principalId, bool exempt, bool authentication)
        {
            if (exempt || authentication) return true;
            return principalId > 0 && globalPrincipalLimiter.TryAcquire(principalId);
        }

        internal static string GetIp(IChannel channel)
        {
            if (channel?.RemoteAddress is not IPEndPoint endpoint) return null;
            IPAddress address = endpoint.Address;
            if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
            return address.ToString();
        }
    }
}
