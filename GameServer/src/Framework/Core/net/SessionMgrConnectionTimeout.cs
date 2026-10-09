using System;
using System.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace Framework.Core.Net
{
    public partial class SessionMgr
    {
        private CancellationTokenSource idleCancellation;
        private Task idleTask;
        private TimeSpan idleTimeout;
        private void StartIdleChecks()
        {
            Stop();
            if (!string.Equals(ConfigurationManager.AppSettings["ServerRole"], "Game", StringComparison.OrdinalIgnoreCase) ||
                bool.TryParse(ConfigurationManager.AppSettings["GatewayMode"], out bool gateway) && gateway) return;
            idleTimeout = TimeSpan.FromSeconds(ReadIdleSetting("GameConnectionIdleTimeoutSeconds", 60));
            int interval = ReadIdleSetting("GameConnectionCheckIntervalSeconds", 5);
            idleCancellation = new CancellationTokenSource();
            idleTask = RunIdleChecksAsync(interval, idleCancellation.Token);
        }
        private static int ReadIdleSetting(string key, int fallback)
        {
            string text = ConfigurationManager.AppSettings[key];
            if (text == null) return fallback;
            if (!int.TryParse(text, out int seconds) || seconds <= 0)
                throw new ConfigurationErrorsException(key + " must be a positive integer.");
            return seconds;
        }
        public void Stop() => idleCancellation?.Cancel();
        public async Task StopAsync()
        {
            Stop();
            if (idleTask != null) await idleTask;
        }
        private async Task RunIdleChecksAsync(int interval, CancellationToken cancellation)
        {
            try
            {
                using var timer = new PeriodicTimer(TimeSpan.FromSeconds(interval));
                while (await timer.WaitForNextTickAsync(cancellation))
                    CheckIdleConnections(cancellation);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
        }
        private void CheckIdleConnections(CancellationToken cancellation)
        {
            foreach (IdSession session in accountSessions.Values)
            {
                if (session.isWebSocket || session.client == null || !session.IsIdle(idleTimeout)) continue;
                var channel = session.client;
                if (!channel.Active) continue;
                channel.EventLoop.Execute(() =>
                {
                    if (cancellation.IsCancellationRequested) return;
                    lock (BindingSync)
                    {
                        if (!IsCurrentConnection(session) || !ReferenceEquals(session.client, channel) ||
                            !channel.Active || !session.IsIdle(idleTimeout)) return;
                        NLog.LogManager.GetCurrentClassLogger().Info(
                            $"Closing idle TCP session: account[{session.accountId}] player[{session.playerId}] idleSeconds[{session.IdleSeconds:F1}]");
                        channel.CloseAsync();
                    }
                });
            }
        }
    }
}
