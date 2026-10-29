using Game.Core.GM_MailMessage;
using Game.Entries.Modules;
using System;

namespace Game.Debug
{
    public static class MailDebugTool
    {
        private static readonly NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        public static int SendNewPlayerMails(long playerId)
        {
            if (playerId <= 0) throw new ArgumentOutOfRangeException(nameof(playerId));

            int sent = 0;
            for (int i = 1; i <= 10; i++)
            {
                try
                {
                    MailReward[] rewards = i switch
                    {
                        8 => new[] { new MailReward { ItemId = 10001, Quantity = 1 } },
                        9 => new[] { new MailReward { ItemId = 10001, Quantity = 10 } },
                        10 => new[]
                        {
                            new MailReward { ItemId = 10001, Quantity = 5 },
                            new MailReward { ItemId = 10002, Quantity = 20 }
                        },
                        _ => Array.Empty<MailReward>()
                    };
                    string rewardText = i switch
                    {
                        8 => "抽卡券×1",
                        9 => "抽卡券×10",
                        10 => "抽卡券×5、钻石×20",
                        _ => null
                    };
                    var result = MailModule.Instance.SendMail(new MailSendCommand
                    {
                        PlayerId = playerId,
                        SenderId = -1,
                        MailType = rewards.Length > 0 ? 1 : 0,
                        Title = rewards.Length > 0
                            ? $"新玩家奖励测试邮件 {i}/10（{rewardText}）"
                            : $"新玩家测试邮件 {i}/10",
                        Body = rewards.Length > 0
                            ? $"欢迎来到游戏！附件包含{rewardText}，可用于测试单封领取、一键领取和抽奖。请在有效期内领取。"
                            : $"欢迎来到游戏！这是第 {i} 封测试邮件，可用于测试阅读、刷新和删除功能。",
                        LifetimeSeconds = 86400,
                        SourceType = 0,
                        SourceKey = rewards.Length > 0
                            ? $"debug:new-player-reward-mail:v1:{i}"
                            : $"debug:new-player-mail:v1:{i}",
                        Rewards = rewards
                    });

                    if (result.Status == MailStatus.Success) sent++;
                    else logger.Warn($"New player test mail failed: playerId[{playerId}], index[{i}], status[{result.Status}]");
                }
                catch (Exception exception)
                {
                    // Mail failures must not turn an already committed character creation into a login failure.
                    logger.Error(exception, $"New player test mail failed: playerId[{playerId}], index[{i}]");
                }
            }

            logger.Info($"New player test mails: playerId[{playerId}], succeeded[{sent}/10]");
            return sent;
        }
    }
}
