using Framework.Core.Net;
using Framework.Core.task;
using Game.Datas.Messages;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace GameServerTests.Net;

public sealed class MessageTaskExceptionTests
{
    [Theory]
    [InlineData("tcp")]
    [InlineData("http")]
    [InlineData("logic")]
    public void ReflectionFailureLogsUnderlyingExceptionAndHandlerWithoutRequestBody(string transport)
    {
        LoggingConfiguration previous = LogManager.Configuration;
        var target = new MemoryTarget { Layout = "${message}" };
        var configuration = new LoggingConfiguration();
        configuration.AddRule(LogLevel.Error, LogLevel.Fatal, target, "Framework.Core.task.*");
        var handler = new ThrowingHandler();
        try
        {
            LogManager.Configuration = configuration;
            switch (transport)
            {
                case "tcp":
                    MessageTask.Create(1, handler, typeof(ThrowingHandler).GetMethod(nameof(ThrowingHandler.HandleTcp)),
                        new object[] { new ReqGameAuthenticate { gameTicket = "private-ticket-must-not-be-logged" } },
                        new IdSession { accountId = 23, playerId = 42 }, 17, 99).DoAction();
                    break;
                case "http":
                    HttpMessageTask.Create(handler, typeof(ThrowingHandler).GetMethod(nameof(ThrowingHandler.HandleHttp)),
                        Array.Empty<object>(), null).DoAction();
                    break;
                case "logic":
                    LogicTask.Create(1, 2, handler, typeof(ThrowingHandler).GetMethod(nameof(ThrowingHandler.HandleLogic))).DoAction();
                    break;
            }
            LogManager.Flush();
            string entry = Assert.Single(target.Logs);
            Assert.Contains("System.InvalidOperationException: Simulated business failure.", entry);
            Assert.Contains(nameof(ThrowingHandler), entry);
            Assert.Contains(" at ", entry);
            Assert.DoesNotContain("private-ticket-must-not-be-logged", entry);
            if (transport == "tcp")
            {
                Assert.Contains("accountId=23", entry);
                Assert.Contains("playerId=42", entry);
                Assert.Contains("requestId=99", entry);
            }
        }
        finally { LogManager.Configuration = previous; }
    }

    public sealed class ThrowingHandler
    {
        public object HandleTcp(ReqGameAuthenticate request) => throw new InvalidOperationException("Simulated business failure.");
        public object HandleHttp() => throw new InvalidOperationException("Simulated business failure.");
        public void HandleLogic() => throw new InvalidOperationException("Simulated business failure.");
    }
}
