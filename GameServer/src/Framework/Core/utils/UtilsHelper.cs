namespace Framework.Core.Utils {
    using System;
    using System.Security.Cryptography;
    using System.Text;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;

    public class UtilsHelper {
        public static string ProcessDirectory
        {
            get
            {
#if NETSTANDARD2_0 || NETCOREAPP3_1_OR_GREATER || NET5_0_OR_GREATER
                return AppContext.BaseDirectory;
#else
                return AppDomain.CurrentDomain.BaseDirectory;
#endif
            }
        }

        public static void WriteShortLE(byte[] data, int offset, short value) {
            data[offset + 0] = (byte)((value & 0x00ff));
            data[offset + 1] = (byte)((value & 0xff00) >> 8);
        }

        public static void WriteUintLE(byte[] data, int offset, uint value) {
            data[offset + 0] = (byte)((value & 0x000000ff));
            data[offset + 1] = (byte)((value & 0x0000ff00) >> 8);

            data[offset + 2] = (byte)((value & 0x00ff0000) >> 16);
            data[offset + 3] = (byte)((value & 0xff000000) >> 24);
        }

        public static void WriteBytes(byte[] dst, int offset, byte[] src) {
            Array.Copy(src, 0, dst, offset, src.Length);
        }

        public static short ReadShortLE(byte[] data, int offset) {
            short value = (short)(((int)(data[offset + 1]) << 8) | ((int)(data[offset + 0])));
            return value;
        }

        public static uint ReadUintLE(byte[] data, int offset) {
            uint value = (uint)((data[offset + 3] << 24) | (data[offset + 2] << 16) | (data[offset + 1] << 8) | (data[offset + 0]));
            return value;
        }

        public static long ReadLongLE(byte[] data, int offset)
        {
            long value = 0;
            value |= ((long)(data[offset + 0]));
            value |= ((long)(data[offset + 1]) << 8);
            value |= ((long)(data[offset + 2]) << 16);
            value |= ((long)(data[offset + 3]) << 24);
            value |= ((long)(data[offset + 4]) << 32);
            value |= ((long)(data[offset + 5]) << 40);
            value |= ((long)(data[offset + 6]) << 48);
            value |= ((long)(data[offset + 7]) << 56);

            return value;
        }

        public static void WriteULongLE(byte[] data, int offset, ulong value)
        {
            data[offset + 0] = (byte)((value & 0x00000000000000ff));
            data[offset + 1] = (byte)((value & 0x000000000000ff00) >> 8);

            data[offset + 2] = (byte)((value & 0x0000000000ff0000) >> 16);
            data[offset + 3] = (byte)((value & 0x00000000ff000000) >> 24);

            data[offset + 4] = (byte)((value & 0x000000ff00000000) >> 32);
            data[offset + 5] = (byte)((value & 0x0000ff0000000000) >> 40);

            data[offset + 6] = (byte)((value & 0x00ff000000000000) >> 48);
            data[offset + 7] = (byte)((value & 0xff00000000000000) >> 56);
        }

        public static string Md5(string str)
        {
            string cl = str;
            StringBuilder md5_builder = new StringBuilder();
            MD5 md5 = MD5.Create();//实例化一个md5对像
                                   // 加密后是一个字节类型的数组，这里要注意编码UTF8/Unicode等的选择　
            byte[] s = md5.ComputeHash(Encoding.UTF8.GetBytes(cl));
            // 通过使用循环，将字节类型的数组转换为字符串，此字符串是常规字符格式化所得
            for (int i = 0; i < s.Length; i++)
            {
                // 将得到的字符串使用十六进制类型格式。格式后的字符是小写的字母，如果使用大写（X）则格式后的字符是大写字符
                md5_builder.Append(s[i].ToString("X2"));
                //pwd = pwd + s[i].ToString("X");
            }
            return md5_builder.ToString();
        }

        public static long Timestamp() {
            // long timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            // return timestamp;
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            TimeSpan offset = DateTime.Now - epoch;


            return (long)offset.TotalSeconds;
            
        }

        public static long TimestampToday() {
            DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            DateTime today = DateTime.Today;

            TimeSpan offset = today - epoch;

            
            return (long)offset.TotalSeconds;
        }

        public static long TimestampYesterday() {
            long time = TimestampToday();

            return (time - 24 * 60 * 60);
        }

        public static DateTime TimestampToLocalDateTime(long timestamp) {

            DateTimeOffset dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(timestamp);
            // return dateTimeOffset.DateTime; // 标准时区
            return dateTimeOffset.LocalDateTime; // 本地时区
        }


    }
}

