using Framework.Core.Net;
using Framework.Core.Utils;
using Game.Core.EntityMgr;
using Game.Core.GM_RedDot;
using Game.Datas.Excels;
using Game.Datas.Messages;
using System;
using System.Linq;

namespace Game.Entries.Modules
{
    public sealed class RedDotModule
    {
        public static RedDotModule Instance { get; } = new();
        public void Init() => GM_RedDotMgr.Instance.Configure(
            new RedDotCatalog(ExcelUtils.GetConfigDatas<RedDotConfig>(null)),
            result => new NotifyRedDotChanged { configVersion = result.ConfigVersion,
                stateVersion = result.StateVersion, states = MapStates(result) });

        public ResRedDotInit Initialize(IdSession session, ReqRedDotInit request, long playerId)
        {
            if (!IsCurrent(session, playerId)) return new() { status = (int)Respones.AccountIsNotLogin };
            if (request == null) return new() { status = (int)Respones.InvalidParams };
            try
            {
                var result = GM_RedDotMgr.Instance.Initialize(playerId);
                return new() { status = (int)Respones.OK, configVersion = result.ConfigVersion,
                    stateVersion = result.StateVersion, states = MapStates(result),
                    nodes = GM_RedDotMgr.Instance.Catalog.Nodes.Select(n => new RedDotNodeMessage
                    { id = n.id, name = n.name, parentId = n.parentId, clearType = n.clearType,
                        enabled = n.enabled, tipVersion = n.tipVersion }).ToList() };
            }
            catch (Exception ex) { NLog.LogManager.GetCurrentClassLogger().Error(ex, "Red-dot initialization failed.");
                return new() { status = (int)Respones.SystemErr }; }
        }

        public ResRedDotView MarkViewed(IdSession session, ReqRedDotView request, long playerId)
        {
            if (!IsCurrent(session, playerId)) return new() { status = (int)Respones.AccountIsNotLogin };
            if (request == null || !GM_RedDotMgr.Instance.Catalog.CanView(request.nodeId, request.tipVersion))
                return new() { status = (int)Respones.InvalidParams };
            try
            {
                var result = GM_RedDotMgr.Instance.MarkViewed(playerId, request.nodeId, request.tipVersion);
                return new() { status = (int)Respones.OK, configVersion = result.ConfigVersion,
                    stateVersion = result.StateVersion, states = MapStates(result) };
            }
            catch (Exception ex) { NLog.LogManager.GetCurrentClassLogger().Error(ex, "Red-dot view failed.");
                return new() { status = (int)Respones.SystemErr }; }
        }
        private static bool IsCurrent(IdSession session, long playerId) => playerId > 0 && session != null &&
            session.playerId == playerId && ReferenceEquals(GM_EntityMgr.Instance.Get(playerId)?.uPlayer.s, session);
        private static System.Collections.Generic.List<RedDotStateMessage> MapStates(RedDotSnapshot result) =>
            result.States.Select(s => new RedDotStateMessage { nodeId = s.NodeId, isActive = s.IsActive }).ToList();
    }
}
