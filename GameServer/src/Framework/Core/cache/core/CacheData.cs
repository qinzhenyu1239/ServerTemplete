using System;

namespace Framework.Core.Cache {
    public class CacheData<T>
    {
        /// <summary>
        /// 访问时间
        /// </summary>
        public DateTime AccessTime { get; set; }

        /// <summary>
        /// 写入时间
        /// </summary>
        public DateTime WriteTime { get; set; }

        ///// <summary>
        ///// 缓存数据
        ///// </summary>
        //public T Data { get; set; }

        /// <summary>
        /// 缓存键
        /// </summary>
        public string Key { get; set; }

        public T Original { get; set; }
    }
}
