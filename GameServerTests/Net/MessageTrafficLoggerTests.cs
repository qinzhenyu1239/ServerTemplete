using Framework.Core.Net;
using Framework.Core.Serializer;
using Game.Datas.Messages;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace GameServerTests.Net;

public sealed class MessageTrafficLoggerTests
{
    [Fact]
    public void NLogConfiguration_WritesSummaryToFileAndConsoleWithoutDetailedLogger()
    {
        string configPath = Path.Combine(AppContext.BaseDirectory, "nlog.config");
        var configuration = new XmlLoggingConfiguration(configPath);

        Assert.DoesNotContain(configuration.LoggingRules,
            candidate => candidate.LoggerNamePattern == "Framework.Core.Net.MessageTrafficLogger");

        LoggingRule summaryRule = Assert.Single(configuration.LoggingRules,
            candidate => candidate.LoggerNamePattern == "Framework.Core.Net.MessageTrafficSummary");

        Assert.True(summaryRule.Final);
        Assert.Contains(summaryRule.Targets, target => target.Name == "asyncConsole");
        Assert.Contains(summaryRule.Targets, target => target.Name == "asyncFile");
    }

    [Fact]
    public void Received_LogsOnlyTransportAndProtocolSummary()
    {
        LoggingConfiguration previousConfiguration = LogManager.Configuration;
        var memoryTarget = new MemoryTarget { Layout = "${message}" };
        var configuration = new LoggingConfiguration();
        configuration.AddRule(LogLevel.Info, LogLevel.Fatal, memoryTarget,
            "Framework.Core.Net.MessageTrafficSummary");

        try
        {
            LogManager.Configuration = configuration;
            MessageFactory.Instance.InitMeesagePool();

            const long timestamp = 1_725_888_123_456;
            byte[] packet = MessagePusher.PackMessage(
                new ReqHeartbeat { clientUnixMilliseconds = timestamp },
                routeTag: 101,
                logicServerId: 2,
                requestId: 303);

            MessageTrafficLogger.Received(null, packet, 0, packet.Length);
            LogManager.Flush();

            string line = Assert.Single(memoryTarget.Logs);
            Assert.Contains("[RECV]", line);
            Assert.Contains("module=101", line);
            Assert.Contains("cmd=83", line);
            Assert.Contains("routeTag=101", line);
            Assert.Contains("requestId=303", line);
            Assert.DoesNotContain("messageType=", line);
            Assert.DoesNotContain("body=", line);
            Assert.DoesNotContain("payloadBase64=", line);
            Assert.DoesNotContain(timestamp.ToString(), line);
        }
        finally
        {
            LogManager.Configuration = previousConfiguration;
        }
    }
}
