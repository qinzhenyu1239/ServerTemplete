using System.Runtime.Caching;

namespace Framework.Core.Cache {
    public class DefaultCacheContainer<K, V> : AbstractCacheContainer<K, V>
    {
        private IPersistable<K, V> loader = null;


        public DefaultCacheContainer(IPersistable<K, V> loader) : base() {
            this.loader = loader;
        }
        
        protected override V loadFromDb(K key)
        {
            if (this.loader != null) {
                return this.loader.Load(key);
            }

            return default(V);
        }

        public override void onRemoval(CacheEntryRemovedArguments arguments) {
        }
    }
}




