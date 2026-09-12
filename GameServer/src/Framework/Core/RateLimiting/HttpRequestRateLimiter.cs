using System;

namespace Framework.Core.RateLimiting
{
    internal enum HttpRequestRateLimitKind { Default, Credential, Ticket }

    internal sealed class HttpRequestRateLimiter
    {
        internal static readonly HttpRequestRateLimiter Instance = new();
        private readonly TokenBucketRateLimiter<string> globalIpLimiter = new(60, 20, TimeSpan.FromMinutes(10), StringComparer.Ordinal);
        private readonly TokenBucketRateLimiter<string> credentialLimiter = new(5, 0.2, TimeSpan.FromMinutes(15), StringComparer.Ordinal);
        private readonly TokenBucketRateLimiter<string> ticketLimiter = new(10, 2, TimeSpan.FromMinutes(10), StringComparer.Ordinal);
        private HttpRequestRateLimiter() { }

        internal bool TryAcquire(string ip, HttpRequestRateLimitKind kind)
        {
            if (string.IsNullOrWhiteSpace(ip) || !globalIpLimiter.TryAcquire(ip)) return false;
            return kind switch
            {
                HttpRequestRateLimitKind.Credential => credentialLimiter.TryAcquire(ip),
                HttpRequestRateLimitKind.Ticket => ticketLimiter.TryAcquire(ip),
                _ => true
            };
        }
    }
}
