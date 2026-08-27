using DotNetty.Buffers;
using DotNetty.Codecs.Http.WebSockets;
using DotNetty.Transport.Channels;
using System.Net;

namespace Framework.Core.Net {
    public class IdSession {

        public IChannel client = null;

        public long distributeKey = 0; //  线程池分发器的索引, 每个IdSession都对应唯一的编号; 

        public long playerId = 0; // 玩家对应的游戏ID号;

        public long accountId = 0; // 玩家对应的账号ID;

        public long accountIdAndJob = 0; // 玩家Id与职业所对应的key;

        public bool isWebSocket = false; // 当前的session是WebSocket还是TcpSocket;

        public int logicServerId = -1; // 当前玩家分配的logicServerId;

        public short machineId = -1; // 网关模式下使用，要转发的GameServer机器Id

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

            // WebSockt:  Text文本模式丢弃，arraybuffer 二进制模式;
            if (this.isWebSocket)
            {
                BinaryWebSocketFrame f = new BinaryWebSocketFrame(buffer);
                this.client.WriteAndFlushAsync(f); // eventLoop线程里面
                /*this.client.EventLoop.Execute(async () =>
                {
                    await this.client.WriteAndFlushAsync(f);
                });*/
            }
            else
            {  // TcpSocket的Sender ---> IByteBuffer数据对象，在Pipeline里面处理;
                this.client.WriteAndFlushAsync(buffer); // eventLoop线程里面
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

            // WebSockt:  Text文本模式丢弃，arraybuffer 二进制模式;
            if (this.isWebSocket) {
                BinaryWebSocketFrame f = new BinaryWebSocketFrame(buffer);
                this.client.WriteAndFlushAsync(f); // eventLoop线程里面
                /*this.client.EventLoop.Execute(async () =>
                {
                    await this.client.WriteAndFlushAsync(f);
                });*/
            }
            else {  // TcpSocket的Sender ---> IByteBuffer数据对象，在Pipeline里面处理;
                this.client.WriteAndFlushAsync(buffer); // eventLoop线程里面
                /*this.client.EventLoop.Execute(async () =>
                {
                    // this.client.WriteAndFlushAsync(buffer);
                    await this.client.WriteAndFlushAsync(buffer);
                });*/
            }
        }
    }
}
