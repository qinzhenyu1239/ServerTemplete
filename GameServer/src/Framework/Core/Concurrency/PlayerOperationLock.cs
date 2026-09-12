using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Framework.Core.Concurrency
{
    public static class PlayerOperationLock
    {
        private static readonly ConcurrentDictionary<long, Entry> Entries = new();

        public static Lease Acquire(long playerId)
        {
            while (true)
            {
                Entry entry = Entries.GetOrAdd(playerId, _ => new Entry());
                lock (entry.LifecycleGate)
                {
                    if (entry.IsRemoved) continue;
                    entry.ReferenceCount++;
                    return new Lease(playerId, entry);
                }
            }
        }

        private static void Release(long playerId, Entry entry)
        {
            lock (entry.LifecycleGate)
            {
                entry.ReferenceCount--;
                if (entry.ReferenceCount != 0) return;
                entry.IsRemoved = true;
                ((ICollection<KeyValuePair<long, Entry>>)Entries)
                    .Remove(new KeyValuePair<long, Entry>(playerId, entry));
            }
        }

        internal sealed class Entry
        {
            public object LifecycleGate { get; } = new();
            public object SyncRoot { get; } = new();
            public int ReferenceCount { get; set; }
            public bool IsRemoved { get; set; }
        }

        public sealed class Lease : IDisposable
        {
            private readonly long playerId;
            private Entry entry;

            internal Lease(long playerId, Entry entry)
            {
                this.playerId = playerId;
                this.entry = entry;
            }

            public object SyncRoot => entry.SyncRoot;

            public void Dispose()
            {
                Entry current = entry;
                if (current == null) return;
                entry = null;
                Release(playerId, current);
            }
        }
    }
}
