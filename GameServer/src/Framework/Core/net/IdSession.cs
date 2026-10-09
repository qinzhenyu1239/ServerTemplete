using DotNetty.Buffers;
using DotNetty.Codecs.Http.WebSockets;
using DotNetty.Transport.Channels;
using System.Net;

namespace Framework.Core.Net {
    public class IdSession {

        public IChannel client = null;
        private long lastValidActivity = System.Diagnostics.Stopwatch.GetTimestamp();
        public void MarkValidActivity() => System.Threading.Interlocked.Exchange(
            ref lastValidActivity, System.Diagnostics.Stopwatch.GetTimestamp());
        public double IdleSeconds => (System.Diagnostics.Stopwatch.GetTimestamp() -
            System.Threading.Interlocked.Read(ref lastValidActivity)) / (double)System.Diagnostics.Stopwatch.Frequency;
        public bool IsIdle(System.TimeSpan timeout) => IdleSeconds >= timeout.TotalSeconds;
        
        public long distributeKey = 0; //  �̳߳طַ���������, ÿ��IdSession����ӦΨһ�ı��; 

        public long playerId = 0; // ��Ҷ�Ӧ����ϷID��;

        public long accountId = 0; // ��Ҷ�Ӧ���˺�ID;

        public string authSessionId = null;

        public long accountIdAndJob = 0; // ���Id��ְҵ����Ӧ��key;

        public bool isWebSocket = false; // ��ǰ��session��WebSocket����TcpSocket;

        public int logicServerId = -1; // ��ǰ��ҷ����logicServerId;

        public short machineId = -1; // ����ģʽ��ʹ�ã�Ҫת����GameServer����Id

        public string GetIp() {
            return ((IPEndPoint)this.client.RemoteAddress).Address.ToString().Substring(7);
        }

        public string GetRemoteAddress()
        {
            return (this.client.RemoteAddress.ToString());
        }

        public void Send(byte[] data, int offset, int count) {
            if (this.client == null) {
                return;
            }

            MessageTrafficLogger.Sent(this, data, offset, count);

            IByteBuffer buffer = Unpooled.Buffer();
            buffer.WriteBytes(data, offset, count);
            // buffer.WriteBytes(data);

            // WebSockt:  Text�ı�ģʽ������arraybuffer ������ģʽ;
            if (this.isWebSocket)
            {
                BinaryWebSocketFrame f = new BinaryWebSocketFrame(buffer);
                this.client.WriteAndFlushAsync(f); // eventLoop�߳�����
                /*this.client.EventLoop.Execute(async () =>
                {
                    await this.client.WriteAndFlushAsync(f);
                });*/
            }
            else
            {  // TcpSocket��Sender ---> IByteBuffer���ݶ�����Pipeline���洦��;
                this.client.WriteAndFlushAsync(buffer); // eventLoop�߳�����
                /*this.client.EventLoop.Execute(async () =>
                {
                    // this.client.WriteAndFlushAsync(buffer);
                    await this.client.WriteAndFlushAsync(buffer);
                });*/
            }
        }

        public void Send(byte[] data)
        {
            if (this.client == null) {
                return;
            }

            MessageTrafficLogger.Sent(this, data, 0, data?.Length ?? 0);

            IByteBuffer buffer = Unpooled.Buffer();
            buffer.WriteBytes(data);

            // WebSockt:  Text�ı�ģʽ������arraybuffer ������ģʽ;
            if (this.isWebSocket) {
                BinaryWebSocketFrame f = new BinaryWebSocketFrame(buffer);
                this.client.WriteAndFlushAsync(f); // eventLoop�߳�����
                /*this.client.EventLoop.Execute(async () =>
                {
                    await this.client.WriteAndFlushAsync(f);
                });*/
            }
            else {  // TcpSocket��Sender ---> IByteBuffer���ݶ�����Pipeline���洦��;
                this.client.WriteAndFlushAsync(buffer); // eventLoop�߳�����
                /*this.client.EventLoop.Execute(async () =>
                {
                    // this.client.WriteAndFlushAsync(buffer);
                    await this.client.WriteAndFlushAsync(buffer);
                });*/
            }
        }
    }
}
