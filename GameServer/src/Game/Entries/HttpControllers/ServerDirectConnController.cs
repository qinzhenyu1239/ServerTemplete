using DotNetty.Codecs.Http;
using Framework.Core.Utils;
using Game.Entries.Modules;

namespace Game.Entries.HttpControllers
{
    [HttpController]
    internal sealed class ServerDirectConnController
    {
        [HttpRequestMapping("/PullingServerDirectConnConfig")]
        public object DoPullServerDirectConnConfig(IFullHttpRequest request) =>
            ServerDirectConnModule.Instance.HandlerPullServerDirectConnConfig();
    }
}
