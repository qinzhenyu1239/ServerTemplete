using DotNetty.Codecs.Http;
using Framework.Core.Utils;
using Game.Entries.Modules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Entries.HttpControllers
{
    [HttpController]
    class HttpGameController
    {
        [HttpRequestMapping("/PullingServerZone")]
        public string DoPullServerZoneAction(IFullHttpRequest request) {
            return HttpServerZoneModule.Instance.HandlePullServerZoneAction();
        }

        [HttpRequestMapping("/PullingServerDirectConnConfig")]
        public string DoPullPullingServerDirectConnConfigAction(IFullHttpRequest request)
        {
            return HttpServerDirectConnModule.Instance.HandlePullServerDirectConnConfigAction();
        }
    }
}
