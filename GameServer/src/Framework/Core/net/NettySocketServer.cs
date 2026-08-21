using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Codecs.Http;
using DotNetty.Handlers.Tls;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using Framework.Core.Utils;
using System;
using System.IO;
using System.Net;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;

namespace Framework.Core.Net {
    public class NettySocketServer {

        IEventLoopGroup bossGroup = null;
        IEventLoopGroup workerGroup = null;

        IChannel wsBoundChannel = null;
        IChannel tcpBoundChannel = null;

        IChannel httpBoundChannel = null;


        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();


        public NettySocketServer() {
            this.bossGroup = new MultithreadEventLoopGroup(4);
            this.workerGroup = new MultithreadEventLoopGroup();
        }

        public async System.Threading.Tasks.Task StartSterverAt(int tcpPort, int wsPort, bool IsSsl) {

            ServerBootstrap wsBootstrap = null;
            ServerBootstrap bootstrap = null;
            

            try
            {
                // Tcp Server
                if (tcpPort > 0) {
                    this.logger.Info("netty TcpSocket服务已启动，正在监听用户的请求@port:" + tcpPort + "......");
                    bootstrap = new ServerBootstrap();
                    bootstrap.Group(bossGroup, workerGroup);

                    bootstrap.Channel<TcpServerSocketChannel>();
                    bootstrap.Option(ChannelOption.SoBacklog, 512);

                    bootstrap.ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        pipeline.AddLast("framing-enc", new LengthFieldPrepender(ByteOrder.LittleEndian, 2, 0, true));
                        pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ByteOrder.LittleEndian, ushort.MaxValue, 0, 2, -2, 2, true));

                        pipeline.AddLast("IoEventHandler", new TcpSocketServerHandler());
                    }));
                    this.tcpBoundChannel = await bootstrap.BindAsync(tcpPort);
                }
                // end Tcp Server

                // WebSocket Server
                if (wsPort > 0) {
                    this.logger.Info("netty WebSocket 服务已启动，正在监听用户的请求@port:" + wsPort + "......");
                    X509Certificate2 tlsCertificate = null;
                    if (IsSsl) // 注: ssl为调试，等上线加证书的时候我们来处理;
                    {
                        tlsCertificate = new X509Certificate2(Path.Combine(UtilsHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
                    }
                    wsBootstrap = new ServerBootstrap();
                    wsBootstrap.Group(this.bossGroup, this.workerGroup);
                    wsBootstrap.Channel<TcpServerSocketChannel>();
                    wsBootstrap.Option(ChannelOption.SoBacklog, 512);
                    wsBootstrap.ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        if (tlsCertificate != null)
                        {
                            pipeline.AddLast(TlsHandler.Server(tlsCertificate));
                        }
                        pipeline.AddLast(new HttpServerCodec());
                        // 加了这个，消息就会组合在IFullHttpRequest里面;
                        pipeline.AddLast(new HttpObjectAggregator(65536));
                        pipeline.AddLast(new WebSocketServerHandler());
                    }));
                    // IChannel wsBoundChannel = await wsBootstrap.BindAsync(IPAddress.Loopback, wsPort);
                    wsBoundChannel = await wsBootstrap.BindAsync(wsPort);
                }
                // end
            }
            finally
            {
               
            }
        }

        public async System.Threading.Tasks.Task StartGatewayServerAt(int tcpPort, int wsPort, bool IsSsl)
        {

            ServerBootstrap wsBootstrap = null;
            ServerBootstrap bootstrap = null;


            try
            {
                // Tcp Server
                if (tcpPort > 0)
                {
                    this.logger.Info("netty TcpSocket服务已启动，正在监听用户的请求@port:" + tcpPort + "......");
                    bootstrap = new ServerBootstrap();
                    bootstrap.Group(bossGroup, workerGroup);

                    bootstrap.Channel<TcpServerSocketChannel>();
                    bootstrap.Option(ChannelOption.SoBacklog, 512);

                    bootstrap.ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        pipeline.AddLast("framing-enc", new LengthFieldPrepender(ByteOrder.LittleEndian, 2, 0, true));
                        pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ByteOrder.LittleEndian, ushort.MaxValue, 0, 2, -2, 2, true));

                        pipeline.AddLast("IoEventHandler", new TcpSocketServerHandler(true));
                    }));
                    this.tcpBoundChannel = await bootstrap.BindAsync(tcpPort);
                }
                // end Tcp Server

                // WebSocket Server
                if (wsPort > 0)
                {
                    this.logger.Info("netty WebSocket 服务已启动，正在监听用户的请求@port:" + wsPort + "......");
                    X509Certificate2 tlsCertificate = null;
                    if (IsSsl) // 注: ssl为调试，等上线加证书的时候我们来处理;
                    {
                        tlsCertificate = new X509Certificate2(Path.Combine(UtilsHelper.ProcessDirectory, "dotnetty.com.pfx"), "password");
                    }
                    wsBootstrap = new ServerBootstrap();
                    wsBootstrap.Group(this.bossGroup, this.workerGroup);
                    wsBootstrap.Channel<TcpServerSocketChannel>();
                    wsBootstrap.Option(ChannelOption.SoBacklog, 512);
                    wsBootstrap.ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        if (tlsCertificate != null)
                        {
                            pipeline.AddLast(TlsHandler.Server(tlsCertificate));
                        }
                        pipeline.AddLast(new HttpServerCodec());
                        pipeline.AddLast(new HttpObjectAggregator(65536));
                        pipeline.AddLast(new WebSocketServerHandler(true));
                    }));
                    // IChannel wsBoundChannel = await wsBootstrap.BindAsync(IPAddress.Loopback, wsPort);
                    wsBoundChannel = await wsBootstrap.BindAsync(wsPort);
                }
                // end
            }
            finally
            {

            }
        }

        public async System.Threading.Tasks.Task StartGwToServerSocketAt(int gwPort)
        {
            ServerBootstrap bootstrap = null;

            try
            {
                // Gateway Server
                if (gwPort > 0)
                {
                    this.logger.Info("[Gateway To GameServer Socket服务已启动，正在监听用户的请求@port:" + gwPort + "......]");
                    bootstrap = new ServerBootstrap();
                    bootstrap.Group(bossGroup, workerGroup);

                    bootstrap.Channel<TcpServerSocketChannel>();
                    bootstrap.Option(ChannelOption.SoBacklog, 512);

                    bootstrap.ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        pipeline.AddLast("framing-enc", new LengthFieldPrepender(ByteOrder.LittleEndian, 2, 0, true));
                        pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ByteOrder.LittleEndian, ushort.MaxValue, 0, 2, -2, 2, true));

                        pipeline.AddLast("IoEventHandler", new GwToGameServerSocketHandler());
                    }));
                    this.tcpBoundChannel = await bootstrap.BindAsync(gwPort);
                }
                // end Tcp Server

              
            }
            finally
            {

            }
        }

        public async Task StartHttpServer(int port, bool IsSsl = false) {
            if (port <= 1024) {
                return;
            }

            this.logger.Info("netty HttpServer 服务已启动，正在监听用户的请求@port:" + port + "......");

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                GCSettings.LatencyMode = GCLatencyMode.SustainedLowLatency;
            }

            X509Certificate2 tlsCertificate = null;
            if (IsSsl) // 注: ssl为调试，等上线加证书的时候我们来处理;
            {
                tlsCertificate = new X509Certificate2(Path.Combine("", "dotnetty.com.pfx"), "password");
            }

            var bootstrap = new ServerBootstrap();
            bootstrap.Group(this.bossGroup, this.workerGroup);
            bootstrap.Channel<TcpServerSocketChannel>();
            bootstrap
                    .Option(ChannelOption.SoBacklog, 8192)
                    .ChildHandler(new ActionChannelInitializer<IChannel>(channel =>
                    {
                        IChannelPipeline pipeline = channel.Pipeline;
                        if (tlsCertificate != null)
                        {
                            pipeline.AddLast(TlsHandler.Server(tlsCertificate));
                        }
                        pipeline.AddLast("encoder", new HttpResponseEncoder());
                        pipeline.AddLast("decoder", new HttpRequestDecoder(4096, 8192, 8192, false));
                        pipeline.AddLast(new HttpObjectAggregator(65536));
                        pipeline.AddLast("handler", new HttpServerHandler());
                    }));


            this.httpBoundChannel = await bootstrap.BindAsync(IPAddress.IPv6Any, port);
            
            // this.logger.Info($"Httpd started. Listening on {this.httpBoundChannel.LocalAddress}");
        }


        public async Task Shutdown() {
            if (wsBoundChannel != null)
            {
                await wsBoundChannel.CloseAsync();
            }

            if (this.tcpBoundChannel != null)
            {
                await this.tcpBoundChannel.CloseAsync();
            }


            if (this.httpBoundChannel != null)
            {
                await this.httpBoundChannel.CloseAsync();
            }

            await Task.WhenAll(
                   bossGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)),
                   workerGroup.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1)));
        }
    }
}

