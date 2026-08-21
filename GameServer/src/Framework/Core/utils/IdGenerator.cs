

using System;
using System.Threading;

namespace Framework.Core.Utils
{
    public class IdGenerator
    {
        private static long i = 0;

        public static long GetNextId(long serverId = 1001) {
            //----------------id格式 -------------------------
            //----------long类型8个字节64个比特位----------------
            // 高16位          	| 中32位          |  低16位
            // serverId        系统秒数          自增长号
            // long serverId = 10001;
            return (serverId << 48)
                    | ((((long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds / 1000) & 0xFFFFFFFF) << 16)
                    | (Interlocked.Increment(ref i) & 0xFFFF);
        }
    }
}
