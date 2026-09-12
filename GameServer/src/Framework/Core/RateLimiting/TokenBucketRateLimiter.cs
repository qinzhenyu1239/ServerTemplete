using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Framework.Core.RateLimiting
{
    internal sealed class TokenBucketRateLimiter<TKey> where TKey : notnull
    {
        private const int SweepInterval = 1024;
        private sealed class Bucket
        {
            internal double Tokens;
            internal long LastRefillTimestamp;
            internal long LastSeenTimestamp;
            internal bool Retired;
            internal Bucket(double tokens, long timestamp)
            {
                Tokens = tokens;
                LastRefillTimestamp = timestamp;
                LastSeenTimestamp = timestamp;
            }
        }

        private readonly ConcurrentDictionary<TKey, Bucket> buckets;
        private readonly double capacity;
        private readonly double tokensPerTimestamp;
        private readonly long idleExpirationTimestamps;
        private long operationCount;
        private int sweepInProgress;

        internal TokenBucketRateLimiter(int capacity, double tokensPerSecond,
            TimeSpan idleExpiration, IEqualityComparer<TKey> comparer = null)
        {
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (tokensPerSecond <= 0) throw new ArgumentOutOfRangeException(nameof(tokensPerSecond));
            if (idleExpiration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(idleExpiration));
            this.capacity = capacity;
            tokensPerTimestamp = tokensPerSecond / Stopwatch.Frequency;
            idleExpirationTimestamps = checked((long)(idleExpiration.TotalSeconds * Stopwatch.Frequency));
            buckets = new ConcurrentDictionary<TKey, Bucket>(comparer ?? EqualityComparer<TKey>.Default);
        }

        internal bool TryAcquire(TKey key) => TryAcquire(key, Stopwatch.GetTimestamp());

        internal bool TryAcquire(TKey key, long timestamp)
        {
            if (key is null) throw new ArgumentNullException(nameof(key));
            if (timestamp < 0) throw new ArgumentOutOfRangeException(nameof(timestamp));
            if (Interlocked.Increment(ref operationCount) % SweepInterval == 0) RemoveExpiredBuckets(timestamp);
            while (true)
            {
                Bucket bucket = buckets.GetOrAdd(key, _ => new Bucket(capacity, timestamp));
                lock (bucket)
                {
                    if (bucket.Retired) continue;
                    if (timestamp > bucket.LastRefillTimestamp)
                    {
                        bucket.Tokens = Math.Min(capacity, bucket.Tokens +
                            (timestamp - bucket.LastRefillTimestamp) * tokensPerTimestamp);
                        bucket.LastRefillTimestamp = timestamp;
                    }
                    bucket.LastSeenTimestamp = timestamp;
                    if (bucket.Tokens < 1) return false;
                    bucket.Tokens -= 1;
                    return true;
                }
            }
        }

        private void RemoveExpiredBuckets(long timestamp)
        {
            if (Interlocked.Exchange(ref sweepInProgress, 1) != 0) return;
            try
            {
                foreach (KeyValuePair<TKey, Bucket> pair in buckets)
                {
                    Bucket bucket = pair.Value;
                    lock (bucket)
                    {
                        if (bucket.Retired || timestamp - bucket.LastSeenTimestamp < idleExpirationTimestamps) continue;
                        bucket.Retired = true;
                        buckets.TryRemove(pair.Key, out _);
                    }
                }
            }
            finally { Volatile.Write(ref sweepInProgress, 0); }
        }
    }
}
