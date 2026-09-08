using Framework.Core.Utils;
using Framework.Core.Serializer;

namespace Framework.Core.Net
{
    internal static class MessageTrafficLogger
    {
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static void Received(IdSession session, byte[] data, int offset, int count)
        {
            Write("RECV", session, data, offset, count);
        }

        public static void Sent(IdSession session, byte[] data, int offset, int count)
        {
            Write("SEND", session, data, offset, count);
        }

        public static void HttpReceived(string remote, string method, string uri, int count)
        {
            WriteLine($"[MESSAGE] [RECV] " +
                      $"transport=HTTP remote={remote} method={method} uri={uri} bytes={count}");
        }

        public static void HttpSent(string remote, int statusCode, int count)
        {
            WriteLine($"[MESSAGE] [SEND] " +
                      $"transport=HTTP remote={remote} status={statusCode} bytes={count}");
        }

        private static void Write(string direction, IdSession session, byte[] data, int offset, int count)
        {
            string transport = session != null && session.isWebSocket ? "WebSocket" : "TCP";
            string remote = GetRemoteAddress(session);

            if (data == null || offset < 0 || count < ProtocolPacketLayout.HeaderLength || offset > data.Length - count)
            {
                WriteLine($"[MESSAGE] [{direction}] " +
                          $"transport={transport} remote={remote} bytes={count} invalid-header");
                return;
            }

            short module = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.ModuleOffset);
            short cmd = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.CommandOffset);
            long routeTag = UtilsHelper.ReadLongLE(data, offset + ProtocolPacketLayout.RouteTagOffset);
            short logicServerId = UtilsHelper.ReadShortLE(data, offset + ProtocolPacketLayout.LogicServerIdOffset);
            long requestId = UtilsHelper.ReadLongLE(data, offset + ProtocolPacketLayout.RequestIdOffset);

            WriteLine($"[MESSAGE] [{direction}] " +
                      $"transport={transport} remote={remote} module={module} cmd={cmd} " +
                      $"routeTag={routeTag} requestId={requestId} logicServerId={logicServerId} bytes={count}");
        }

        private static string GetRemoteAddress(IdSession session)
        {
            try
            {
                return session?.client?.RemoteAddress?.ToString() ?? "unknown";
            }
            catch
            {
                return "unknown";
            }
        }

        private static void WriteLine(string message)
        {
            Logger.Info(message);
        }
    }
}
