using Framework.Core.Net;
using Framework.Core.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Core.Serializer
{
    public class MessagePusher {
        private const int HEAD_SIZE = 14; // [2, 2, 8, 2]
        public static void PushMessage(IdSession s, Message m, long utag, short logicServerId = 0) {
            byte[] msgBody = SerializerHelper.PBEncoder(m);
            if (msgBody == null) {
                return;
            }

            
            byte[] body = new byte[HEAD_SIZE + msgBody.Length];

            UtilsHelper.WriteShortLE(body, 0, m.GetModule());
            UtilsHelper.WriteShortLE(body, 2, m.GetCmd());
            UtilsHelper.WriteULongLE(body, 4, (ulong)utag);
            UtilsHelper.WriteShortLE(body, 12, (short)logicServerId);
            UtilsHelper.WriteBytes(body, HEAD_SIZE, msgBody);

            s.Send(body); // Tcp, WebSocket
        }

        public static byte[] PackMessage(Message m, long utag, short logicServerId = 0) {
            byte[] msgBody = SerializerHelper.PBEncoder(m);
            if (msgBody == null) {
                return null;
            }


            byte[] body = new byte[HEAD_SIZE + msgBody.Length];

            UtilsHelper.WriteShortLE(body, 0, m.GetModule());
            UtilsHelper.WriteShortLE(body, 2, m.GetCmd());
            UtilsHelper.WriteULongLE(body, 4, (ulong)utag);
            UtilsHelper.WriteShortLE(body, 12, (short)logicServerId);
            UtilsHelper.WriteBytes(body, HEAD_SIZE, msgBody);

            return body;
        }
    }

}
