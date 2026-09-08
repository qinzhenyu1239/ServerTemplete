using System;
using System.Configuration;

namespace Game
{
    public enum ServerRole
    {
        Game,
        Login,
        Gateway,
        ServerZone
    }

    public static class ServerRoleConfig
    {
        public static ServerRole Current
        {
            get
            {
                string configured = ConfigurationManager.AppSettings["ServerRole"];
                if (!string.IsNullOrWhiteSpace(configured) &&
                    Enum.TryParse(configured, true, out ServerRole role))
                {
                    return role;
                }

                return ServerRole.Game;
            }
        }
    }
}
