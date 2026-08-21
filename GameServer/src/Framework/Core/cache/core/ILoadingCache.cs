using System.Collections.Generic;

namespace Framework.Core.Cache {
    public interface ILoadingCache<K, V> {
        /// <summary>
        /// 获取缓存数据
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        V GetIfPresent(K key);

        /// <summary>
        /// 获取数据，缓存没有则从委托查询
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        V Get(K key);

        /// <summary>
        /// 添加缓存
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        void Set(K key, V value);

        /// <summary>
        /// 获取所有有效缓存
        /// </summary>
        /// <param name="keys"></param>
        /// <returns></returns>
        Dictionary<K, V> GetAllPresent(List<K> keys);

        /// <summary>
        /// 是缓存失效
        /// </summary>
        /// <param name="key"></param>
        void Invalidate(K key);
    }

}