using Game.Datas.Excels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Game.Core.GM_RedDot
{
    public enum RedDotClearType { Aggregate = 0, Business = 1, Viewed = 2 }
    public sealed class RedDotValue
    {
        public int NodeId { get; init; }
        public bool IsActive { get; init; }
    }
    public sealed class RedDotSnapshot
    {
        public string ConfigVersion { get; init; }
        public long StateVersion { get; init; }
        public IReadOnlyList<RedDotValue> States { get; init; }
    }
    public sealed class RedDotCatalog
    {
        public IReadOnlyList<RedDotConfig> Nodes { get; }
        public IReadOnlyDictionary<int, RedDotConfig> ById { get; }
        public string Version { get; }

        public RedDotCatalog(IEnumerable<RedDotConfig> source)
        {
            var nodes = source.OrderBy(n => n.id).ToArray();
            var map = new Dictionary<int, RedDotConfig>();
            foreach (var node in nodes)
            {
                if (node.id <= 0 || string.IsNullOrWhiteSpace(node.name) ||
                    !Enum.IsDefined(typeof(RedDotClearType), node.clearType) ||
                    node.tipVersion < 0 || (node.clearType == 2 ? node.tipVersion == 0 : node.tipVersion != 0) ||
                    !map.TryAdd(node.id, node))
                    throw new InvalidOperationException("Invalid or duplicate red-dot node.");
            }
            foreach (var node in nodes)
            {
                var visited = new HashSet<int> { node.id };
                var current = node;
                while (current.parentId != 0)
                {
                    if (!map.TryGetValue(current.parentId, out current) || !visited.Add(current.id))
                        throw new InvalidOperationException("Missing parent or cycle in red-dot configuration.");
                    if (current.clearType != 0)
                        throw new InvalidOperationException("A business red-dot node cannot have children.");
                }
            }
            Nodes = Array.AsReadOnly(nodes);
            ById = map;
            Version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(nodes, new JsonSerializerOptions { IncludeFields = true }))));
        }

        public bool IsEnabled(int id)
        {
            if (!ById.TryGetValue(id, out var node)) return false;
            while (true)
            {
                if (!node.enabled) return false;
                if (node.parentId == 0) return true;
                node = ById[node.parentId];
            }
        }
        public bool CanView(int id, long version) => ById.TryGetValue(id, out var node) &&
            IsEnabled(id) && node.clearType == 2 && version > 0 && version <= node.tipVersion;
    }
}
