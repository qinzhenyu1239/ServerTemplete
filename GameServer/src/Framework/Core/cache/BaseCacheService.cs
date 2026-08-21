namespace Framework.Core.Cache {

    // 注意，不同的Cache存入同一个对象，内部会copy一个对象实例出来;
    public abstract class BaseCacheSerivce<K, V> : IPersistable<K, V>
    {
        AbstractCacheContainer<K, V> container = null;

        public BaseCacheSerivce() {
            this.container = new DefaultCacheContainer<K, V>(this);
        }

        public abstract V Load(K k);

        public V Get(K key)
        {
            return this.container.Get(key);
        }

        public V GetIfPresent(K k)
        {
            return this.container.GetIfPresent(k);
        }

        public void Put(K key, V v)
        {
            this.container.Put(key, v);
        }

        public void Remove(K key)
        {
            this.container.Remove(key);
        }
    }
}
