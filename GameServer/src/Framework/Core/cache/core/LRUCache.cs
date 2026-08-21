using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Caching;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Core.Cache {
    public class LRUCache<K, V> {
        private const int DEFAULT_CAPACITY = 255; // 默认缓存多少数据;

        private readonly MemoryCache cache;
        private long _capacity;//缓存容量 

        private ReaderWriterLockSlim _locker;

        private LinkedList<CacheData<K>> _linkedList; // 每条记录，都会有个cache data;

        private CacheEntryRemovedCallback removedCallback; // 当我们MemoryCache删除一条记录的时候，就会回调;



        public LRUCache(CacheBuilder<K, V> cacheBuilder) {
            this._locker = new ReaderWriterLockSlim();
            this.cache = new MemoryCache(Guid.NewGuid().ToString());

            this._capacity = cacheBuilder.InitCapacity > 0 ? cacheBuilder.InitCapacity : DEFAULT_CAPACITY;
            this._linkedList = new LinkedList<CacheData<K>>();

            this.removedCallback += AfterCacheRemove;
            if (cacheBuilder.removalListener != null) {
                removedCallback += cacheBuilder.removalListener;
            }


        }

        public void Set(K key, V value, CacheItemPolicy cacheItemPolicy) {
            _locker.EnterWriteLock();
            try {
                string hashCode = key.GetHashCode().ToString();
                if (cache.Contains(hashCode)) { // 之前已经有了，所以就要调整这个策略，让他排到前面
                    //异步更新LRU顺序表
                    Task.Run(() =>
                    {
                        var linkCache = _linkedList.FirstOrDefault(p => p.Key == hashCode);
                        if (linkCache != null)
                        {
                            LRUReSort(linkCache, true);
                        }
                    });
                }
                else
                { // 创建一个新的 CacheData,插入到最前面;
                    var cacheData = new CacheData<K>() { Original = key, Key = hashCode, AccessTime = DateTime.Now, WriteTime = DateTime.Now };
                    _linkedList.AddFirst(cacheData);
                }

                cacheItemPolicy.RemovedCallback = this.removedCallback;
                this.cache.Set(hashCode, value, cacheItemPolicy);

                // 淘汰掉太久不用的;
                if (_linkedList.Count > _capacity)
                {
                    cache.Remove(_linkedList.Last.Value.Key);
                }
            }
            finally { _locker.ExitWriteLock(); }
        }


        private void LRUReSort(CacheData<K> linkCache, bool write = false) {
            lock (_linkedList) {
                //LRU重排
                _linkedList.Remove(linkCache);
                //修改访问时间
                linkCache.AccessTime = DateTime.Now;
                if (write) {
                    linkCache.WriteTime = DateTime.Now;
                }
                _linkedList.AddFirst(linkCache);
            }
        }


        private void AfterCacheRemove(CacheEntryRemovedArguments arguments) {
            var key = arguments.CacheItem.Key;
            if (!this.cache.Contains(key))
            {
                lock (_linkedList)
                {
                    var linkCache = _linkedList.FirstOrDefault(p => p.Key == key);
                    if (linkCache != null)
                    {
                        _linkedList.Remove(linkCache);
                    }
                }
            }
        }

        public Tuple<V, DateTime, DateTime> Get(K key) {
            _locker.EnterUpgradeableReadLock();
            try {
                DateTime readTime = DateTime.MinValue;
                DateTime writeTime = DateTime.MinValue;
                string hashCode = key.GetHashCode().ToString();
                var v = cache.Get(hashCode);

                var cacheData = _linkedList.FirstOrDefault(p => p.Key == hashCode);
                var pass = (v != null && cacheData != null);

                if (!pass) {
                    return new Tuple<V, DateTime, DateTime>(default(V), readTime, writeTime);
                }

                // 可能要更新一下这个readTime;
                cacheData.AccessTime = DateTime.Now;
                readTime = cacheData.AccessTime;
                writeTime = cacheData.WriteTime;

                
                _locker.EnterWriteLock();
                try
                {
                    LRUReSort(cacheData);
                }
                finally { _locker.ExitWriteLock(); }
                return new Tuple<V, DateTime, DateTime>((V)v, readTime, writeTime);
            }
            finally { _locker.ExitUpgradeableReadLock(); } // 这里要调试一下，看是否走了释放这个锁;
        }

        public bool Remove(K key) {

            // 注意，这里不从链表删除，而是，cache删除回调的时候，再把它的链表里面的节点给他删除
            // AfterCacheRemove
            string hashCode = key.GetHashCode().ToString();
            return cache.Remove(hashCode) != null;


        }

        public bool ContainsKey(string key)
        {
            _locker.EnterReadLock();
            try
            {
                return cache.Contains(key);
            }
            finally { _locker.ExitReadLock(); }
        }

        public long Count
        {
            get
            {
                _locker.EnterReadLock();
                try
                {
                    return cache.GetCount();
                }
                finally { _locker.ExitReadLock(); }
            }
        }

        public long Capacity
        {
            get
            {
                _locker.EnterReadLock();
                try
                {
                    return _capacity;
                }
                finally { _locker.ExitReadLock(); }
            }
            set
            {
                _locker.EnterUpgradeableReadLock();
                try
                {
                    if (value > 0 && _capacity != value)
                    {
                        _locker.EnterWriteLock();
                        try
                        {
                            _capacity = value;
                            while (_linkedList.Count > _capacity)
                            {
                                var last = _linkedList.LastOrDefault();
                                if (last != null)
                                {
                                    Remove(last.Original);
                                }
                            }
                        }
                        finally { _locker.ExitWriteLock(); }
                    }
                }
                finally { _locker.ExitUpgradeableReadLock(); }
            }
        }

        private List<string> GetCacheKeys()
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var entries = cache.GetType().GetField("_entries", flags).GetValue(cache);
            var cacheItems = entries as IDictionary;
            var keys = new List<string>();
            if (cacheItems == null)
            {
                return keys;
            }
            foreach (DictionaryEntry cacheItem in cacheItems)
            {
                keys.Add(cacheItem.Key.ToString());
            }
            return keys;
        }

        public ICollection<string> Keys
        {
            get
            {
                _locker.EnterReadLock();
                try
                {
                    return GetCacheKeys();
                }
                finally { _locker.ExitReadLock(); }
            }
        }


    }
}
