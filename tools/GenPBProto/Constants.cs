namespace Game{

    enum Module {
        GameLogic1 = 1,
        GameLogic2 = 2,
        /*...*/

        /** 登录 */
        AUTH = 101,
        /** 玩家 */
        PLAYER = 102,
        /** 游戏场景 */
        SCENE = 103,


        /** 活动 */
        ACTIVITY = 104,
        /** 技能 */
        SKILL = 105,
        /** 聊天 */
        CHAT = 106,

        // ------------------跨服业务功能模块（501开始）---------------------
        /** 跨服天梯 */
        LADDER = 501,
    }

    public enum Cmd
    {
        // 游客登录
        eGuestLoginReq = 1,
        eGuestLoginRes,

        // 用户名注册,
        eRegisterUserReq,
        eRegisterUserRes,

        // 用户登录
        eUserLoginReq,
        eUserLoginRes,

        // 游客账号升级
        eGuestUpgradeReq,
        eGuestUpgradeRes,

        // 拉取玩家游戏数据
        ePullingPlayerDataReq,
        ePullingPlayerDataRes,

        // 领取每日登录奖励
        eRecvLoginBonuesReq,
        eRecvLoginBonuesRes,

        // 拉取玩家的奖励数据
        ePullingBonuesListReq,
        ePullingBonuesListRes,

        // 领取玩家的奖励
        eRecvBonuesReq,
        eRecvBonuesRes,

        // 玩家选择角色
        eSelectPlayerReq,
        eSelectPlayerRes,

        // 拉取玩家的任务数据
        ePullingTaskListReq,
        ePullingTaskListRes,

        // 拉取玩家的邮件消息
        ePullingMailMsgReq,
        ePullingMailMsgRes,

        // 玩家更新邮件消息状态
        eUpdateMailMsgReq,
        eUpdategMailMsgRes,

        // 玩家拉取排行
        ePullingRankReq,
        ePullingRankRes,

        // 玩家拉取背包数据
        ePullingPackDataReq,
        ePullingPackDataRes,

        // 玩家交易兑换
        eExchangeProductReq,
        eExchangeProductRes,

        // 玩家获取逻辑服实例Id
        eEnterLogicServerReq,
        eEnterLogicServerRes,

        // 玩家离开逻辑服实例
        eExitLogicServerReq,
        eExitLogicServerRes,

        // 玩家的坐下
        eSitdownReq,
        eSitdownRes,

        // 玩家站起
        eStandupReq,
        eStandupRes,

        // 玩家发送聊天消息
        eTalkMsgReq,
        eTalkMsgRes,

        // 玩家准备好
        ePlayerIsReadyReq,
        ePlayerIsReadyRes,

        // 玩家的游戏操作
        ePlayerOptReq,
        ePlayerOptRes,

        // 房间模式: 服务器主动通知
        eEnterRoomRes,
        eUserArrivedSeatRes,
        eUserExitSeatRes,
        eRoundReadyRes,
        eRoundStartedRes,
        eRoundCheckOutRes,
        eRoundClearRes,
        eReconnRoomRes,
        ePlayerEscapeRes,

        // 开放世界的命令
        // 玩家出生的请求;
        ePlayerSpawnReq,
        ePlayerSpawnRes,

        eNavToDstReq,
        eNavToDstRes,

        eNavInDirReq,
        eNavInDirRes,

        eStartSkillReq,
        eStartSkillRes,

        eStartBuffReq,
        eStartBuffRes,

        // 开放世界: 服务器主动通知
        eEnterAOIRes,
        eLeaveAOIRes,
        eSyncCharactorRes,
        eLostHpRes,

        // 测试正式项目不要直接开放出来
        eTestGetGoodReq,
        eTestGetGoodRes,

        // 测试更新背包物品,正式项目不要直接开放出来
        eTestUpdateGoodsReq,
        eTestUpdateGoddsRes,


        // 测试游戏逻辑服通讯命令
        eTestLogicCmdEchoReq,
        eTestLogicCmdEchoRes,
    }

    public enum QuitReason { 
        PlayerQuit = 1,
        ForceQuit = 2,
        DisconnectQuit = 3,

        // ...
    }

    public enum Respones
    {
        OK = 1,

        
        SystemErr = -100,
        UserIsFreeze = -101,
        UserIsNotGuest = -102,
        InvalidParams = -103,
        UnameIsExist = -104,
        UnameOrUpwdError = -105,
        InvalidOpt = -106,
        PlayerIsNotExist = -107,
        AccountIsNotLogin = -108,
        PlayerIsFreeze = -109,
        AccountIsNotExist = -110,
        MoneyIsNotEnough = -111,
        LogicServerIsBusy = -112,
        InOtherLogicServerInst = -113,
        LogicServerInstIsNotExist = -114,
        UserIsPlaying = -115,
        LevelIsNotEnough = -116,
        ChipIsNotEnough = -117,
        SeatIsUsing = -118,
        // ...
    }


    public enum Channal { 
        InvalidChannal = -1,
        SelfChannal = 0,
        DouYin,
        IosAppStore,
        // ...
    }


    public enum RankType
    {
        WorldCoin = 100001,

        // ...

    }

    public enum RuleType { 
        Task = 1,
        Trading,
        Bonues,
        Backpack,
    }

    public enum ServerType { 
        Logic1 = 1,
        Logic2 = 2,
        Logic2Indep = 3, // Logic2的副本

    }
}

