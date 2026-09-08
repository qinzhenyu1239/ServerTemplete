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
        public static void PushMessage(IdSession s, Message m, long routeTag, short logicServerId = 0, long requestId = 0) {
            byte[] msgBody = SerializerHelper.PBEncoder(m);
            if (msgBody == null) {
                return;
            }

            
            byte[] body = new byte[ProtocolPacketLayout.HeaderLength + msgBody.Length];

            UtilsHelper.WriteShortLE(body, ProtocolPacketLayout.ModuleOffset, m.GetModule());
            UtilsHelper.WriteShortLE(body, ProtocolPacketLayout.CommandOffset, m.GetCmd());
            UtilsHelper.WriteULongLE(body, ProtocolPacketLayout.RouteTagOffset, (ulong)routeTag);
            UtilsHelper.WriteShortLE(body, ProtocolPacketLayout.LogicServerIdOffset, logicServerId);
            UtilsHelper.WriteULongLE(body, ProtocolPacketLayout.RequestIdOffset, (ulong)requestId);
            UtilsHelper.WriteBytes(body, ProtocolPacketLayout.HeaderLength, msgBody);

            s.Send(body); // Tcp, WebSocket
        }

        public static byte[] PackMessage(Message m, long routeTag, short logicServerId = 0, long requestId = 0) {
            byte[] msgBody = SerializerHelper.PBEncoder(m);
            if (msgBody == null) {
                return null;
            }


            byte[] body = new byte[ProtocolPacketLayout.HeaderLength + msgBody.Length];

            UtilsHelper.WriteShortLE(body, ProtocolPacketLayout.ModuleOffset, m.GetModule());
            UtilsHelper.WriteShortLE(body, ProtocolPacketLayout.CommandOffset, m.GetCmd());
            UtilsHelper.WriteULongLE(body, ProtocolPacketLayout.RouteTagOffset, (ulong)routeTag);
            UtilsHelper.WriteShortLE(body, ProtocolPacketLayout.LogicServerIdOffset, logicServerId);
            UtilsHelper.WriteULongLE(body, ProtocolPacketLayout.RequestIdOffset, (ulong)requestId);
            UtilsHelper.WriteBytes(body, ProtocolPacketLayout.HeaderLength, msgBody);

            return body;
        }
    }

}
