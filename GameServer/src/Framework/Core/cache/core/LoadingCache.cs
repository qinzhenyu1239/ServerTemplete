using System;
using System.Collections.Generic;
using System.Runtime.Caching;
using System.Threading.Tasks;
using System.Timers;


namespace Framework.Core.Cache {

    public class LoadingCache<K, V> : ILoadingCache<K, V>
    {
        private readonly CacheBuilder<K, V> cacheBuilder;
        private LRUCache<K, V> lruCache;

        private Func<K, V> load; // 当cache里面没有数据的时候，调用这个函数去加载

        private bool lockState = false;

        private static int IntervalMillSeconed = 60 * 1000;

        public LoadingCache(CacheBuilder<K, V> cacheBuilder, Func<K, V> load) {
            this.cacheBuilder = cacheBuilder;
            this.load = load;
            this.lruCache = new LRUCache<K, V>(cacheBuilder);
            this.AutoReSize();
        }

        public V Get(K key)
        {
            var cache = GetCacheData(key);
            if (cache.Item1 != null && cache.Item2)
            {
                return cache.Item1;
            }

            if (cache.Item1 == null)
            {
                //压根没缓存,全部阻塞
                return GetCacheFromLoad(key);
            }

            if (cache.Item2)
            {
                return cache.Item1;
            }
            //异步刷新未配置，全部阻塞，重新获取
            if (this.cacheBuilder.RefreshAfterWriteMilliseconds <= 0)
            {
                return GetCacheFromLoad(key);
            }

            //异步刷新缓存
            if (this.cacheBuilder.AutoRefresh)
            {
                //自动刷新的，直接后台线程处理，直接返回旧值即可
                Task.Run(() =>
                {
                    if (!lockState)
                    {
                        //Console.WriteLine("后台执行缓存刷新，当前缓存值为：" + cache.Item1.ToString()+", 线程Id："+Task.CurrentId);
                        GetCacheFromLoad(key);
                    }
                });

                return cache.Item1;
            }

            //启用一个线程刷新，其他线程不阻塞
            if (!lockState)
            {
                return GetCacheFromLoad(key);
            }

            return cache.Item1;
        }

        /// <summary>
        /// 加载数据
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        private V GetCacheFromLoad(K key)
        {
            V value = default(V);

            //暂时简单处理锁，此处需要根据key值不同获取不同锁
            lock (this)
            {
                lockState = true;
                value = GetIfPresent(key);
                if (null == value)
                {
                    value = load(key);
                    if (value != null)
                    {
                        Set(key, value);
                    }
                }
                lockState = false;
            }
            
            return value;
        }

        public Dictionary<K, V> GetAllPresent(List<K> keys)
        {
            var dic = new Dictionary<K, V>();

            foreach (var key in keys)
            {
                var value = GetIfPresent(key);
                if (value != null)
                {
                    dic.Add(key, value);
                }
            }

            return dic;
        }

        public V GetIfPresent(K key)
        {
            var cache = this.GetCacheData(key);
            if (cache.Item1 == null || !cache.Item2)
            {
                return default(V);
            }

            return cache.Item1;
        }

        private Tuple<V, bool> GetCacheData(K key)
        {
            var value = lruCache.Get(key);
            if (value.Item1 == null)
            {
                return new Tuple<V, bool>(default(V), false);
            }

            var accessT = (DateTime.Now - value.Item2).TotalMilliseconds;
            var writeT = (DateTime.Now - value.Item3).TotalMilliseconds;

            if (cacheBuilder.RefreshAfterWriteMilliseconds > 0 && cacheBuilder.RefreshAfterWriteMilliseconds < writeT)
            {
                //缓存失效
                return new Tuple<V, bool>(value.Item1, false);
            }

            //缓存是否有效
            var cacheValidate = (cacheBuilder.ExpireAfterAccessMilliseconds <= 0 || cacheBuilder.ExpireAfterAccessMilliseconds > accessT)
                && (cacheBuilder.ExpireAfterWriteMilliseconds <= 0 || cacheBuilder.ExpireAfterWriteMilliseconds > writeT);

            //需要根据时间刷新缓存
            if (!cacheValidate)
            {
                //缓存失效，清除缓存
                lruCache.Remove(key);
                return new Tuple<V, bool>(default(V), false);
            }

            return new Tuple<V, bool>(value.Item1, true);
        }

        public void Invalidate(K key)
        {
            lruCache.Remove(key);
        }

        public void Set(K key, V value)
        {
            //设置缓存时间
            var cacheItemPolicy = new CacheItemPolicy();

            var expireTime = Math.Max(cacheBuilder.ExpireAfterAccessMilliseconds, cacheBuilder.ExpireAfterWriteMilliseconds);
            //设置缓存过期时需要注意，如果有设置刷新时间，则缓存不能过期，因为只有单线程更新缓存影响其他线程获取数据
            if (this.cacheBuilder.RefreshAfterWriteMilliseconds <= 0 && expireTime > 0)
            {
                //设置缓存时间
                cacheItemPolicy.SlidingExpiration = TimeSpan.FromMilliseconds(expireTime);
            }
            else
            {
                ////设置了自动刷新缓存，或者未设置缓存时间，缓存永久不失效
                cacheItemPolicy.Priority = CacheItemPriority.NotRemovable;
            }
            lruCache.Set(key, value, cacheItemPolicy);
        }

        private void AutoReSize()
        {
            Task.Run(() =>
            {
                Timer timer = new Timer();
                timer.AutoReset = true;
                timer.Enabled = true;
                timer.Interval = IntervalMillSeconed;
                timer.Elapsed += Timer_Elapsed;
                timer.Start();
            });
        }

        /// 定时刷新,检测内存
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (this.cacheBuilder.AutoResize)
            {
                ///根据内存大小分配最大容量
                //var phyCacheMemoryLimit = cache.PhysicalMemoryLimit;
                //var memoryLimit = cache.CacheMemoryLimit;

                var calSize = 100000; //计算可分配使用量 todo 待完善

                this.lruCache.Capacity = calSize;
            }
        }
    }

}

