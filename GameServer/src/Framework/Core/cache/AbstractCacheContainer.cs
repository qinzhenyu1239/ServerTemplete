using System;
using System.Runtime.Caching;

namespace Framework.Core.Cache {

    public abstract class AbstractCacheContainer<K, V> {
        private LoadingCache<K, V> cache = null;

        public AbstractCacheContainer() {
            var builder = CacheBuilder<K, V>.NewBuilder();
            builder.SetExpireAfterAccessMilliseconds(18000000);
            builder.SetExpireAfterWriteMilliseconds(18000000);
            builder.RemovalListener(this.onRemoval);

            this.cache = builder.Build(DataLoader);

        }

        public virtual V Get(K k)
        {
            try
            {
                return cache.Get(k);
            }
            catch (Exception)
            {
                return default(V);
            }
        }

        public V GetIfPresent(K k)
        {
            try
            {
                return cache.GetIfPresent(k);
            }
            catch (Exception)
            {
                return default(V);
            }
        }

        public void Put(K k, V v)
        {
            cache.Set(k, v);
        }

        public void Remove(K k) {
            cache.Invalidate(k);
        }

        protected V DataLoader(K key)
        {
            return loadFromDb(key);
        }

        protected abstract V loadFromDb(K key);
        
        public virtual void onRemoval(CacheEntryRemovedArguments arguments)
        {

        }
    }

}
