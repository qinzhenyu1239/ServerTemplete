namespace Framework.Core.Cache {
    public interface IPersistable<K, V> { 
        public V Load(K k);
    }

}
