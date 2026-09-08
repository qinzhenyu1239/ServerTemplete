using System;
using System.Threading.Tasks;

class ServerStartup
{
    static async Task<int> Main(string[] args)
    {
        Game.GameServer.Instance.Start();

        Console.ReadLine();

        Console.WriteLine("Game Server Exit....");

        await Game.GameServer.Instance.Shutdown();

        return 0;
    }
}
