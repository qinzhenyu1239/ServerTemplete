using DotNetty.Codecs.Http;
using Framework.Core.Utils;
using Game.Entries.Modules;

namespace Game.Entries.HttpControllers
{
    [HttpController]
    internal sealed class ServerZoneController
    {
        [HttpRequestMapping("/PullingServerZone")]
        public object DoPullServerZone(IFullHttpRequest request) =>
            ServerZoneModule.Instance.HandlerPullServerZone();
    }
}
