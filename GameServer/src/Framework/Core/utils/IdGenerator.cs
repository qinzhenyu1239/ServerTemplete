

using System;
using System.Configuration;
using System.Threading;

namespace Framework.Core.Utils
{
    public class IdGenerator
    {
        // 0 | 41-bit timestamp(ms) | 10-bit workerId | 12-bit sequence
        private const int WorkerIdBits = 10;
        private const int SequenceBits = 12;
        private const int WorkerIdShift = SequenceBits;
        private const int TimestampShift = WorkerIdBits + SequenceBits;
        private const long MaxWorkerId = (1L << WorkerIdBits) - 1;
        private const long SequenceMask = (1L << SequenceBits) - 1;
        private const long MaxTimestamp = (1L << 41) - 1;
        private const long AllowedClockRollbackMilliseconds = 5;

        // 2026-01-01T00:00:00Z. A custom epoch preserves more timestamp lifetime.
        private const long EpochUnixMilliseconds = 1767225600000L;

        private static readonly object Sync = new();
        private static readonly Lazy<long> configuredWorkerId = new(ReadConfiguredWorkerId);
        private static long lastTimestamp = -1;
        private static long sequence;

        public static long GetNextId(long? serverId = null) {
            long workerId = serverId ?? configuredWorkerId.Value;
            ValidateWorkerId(workerId);

            lock (Sync)
            {
                long timestamp = CurrentTimestamp();
                if (timestamp < lastTimestamp)
                {
                    long rollback = lastTimestamp - timestamp;
                    if (rollback > AllowedClockRollbackMilliseconds)
                    {
                        throw new InvalidOperationException(
                            $"System clock moved backwards by {rollback}ms; refusing to generate a duplicate ID.");
                    }

                    timestamp = WaitUntil(lastTimestamp);
                }

                if (timestamp == lastTimestamp)
                {
                    sequence = (sequence + 1) & SequenceMask;
                    if (sequence == 0)
                    {
                        timestamp = WaitNextTimestamp(lastTimestamp);
                    }
                }
                else
                {
                    sequence = 0;
                }

                lastTimestamp = timestamp;
                if (timestamp < 0 || timestamp > MaxTimestamp)
                {
                    throw new InvalidOperationException("Snowflake timestamp is outside the supported range.");
                }

                return (timestamp << TimestampShift)
                       | (workerId << WorkerIdShift)
                       | sequence;
            }
        }

        private static long ReadConfiguredWorkerId()
        {
            if (!long.TryParse(ConfigurationManager.AppSettings["ServerId"], out long serverId) ||
                serverId < 0 || serverId > MaxWorkerId)
            {
                throw new ConfigurationErrorsException(
                    $"ServerId is used as the Snowflake workerId and must be between 0 and {MaxWorkerId}.");
            }

            return serverId;
        }

        private static void ValidateWorkerId(long workerId)
        {
            if (workerId < 0 || workerId > MaxWorkerId)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(workerId), workerId, $"Snowflake workerId must be between 0 and {MaxWorkerId}.");
            }
        }

        private static long CurrentTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - EpochUnixMilliseconds;
        }

        private static long WaitUntil(long targetTimestamp)
        {
            long timestamp = CurrentTimestamp();
            while (timestamp < targetTimestamp)
            {
                Thread.SpinWait(100);
                timestamp = CurrentTimestamp();
            }
            return timestamp;
        }

        private static long WaitNextTimestamp(long previousTimestamp)
        {
            long timestamp = CurrentTimestamp();
            while (timestamp <= previousTimestamp)
            {
                Thread.SpinWait(100);
                timestamp = CurrentTimestamp();
            }
            return timestamp;
        }
    }
}
