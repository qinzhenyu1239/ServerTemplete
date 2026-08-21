using Game.Datas.Messages;
using Game.LogicServer;
using ProtoBuf;
using ProtoBuf.Meta;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
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
// #else
//         var opt = new SchemaGenerationOptions
//             {
//
//                 Types = {
//                     typeof(ReqGuestLogin),
//                     typeof(ReqUserLogin),
//                     typeof(ResUserLogin),
//                     typeof(AccountInfo),
//                     typeof(ResGuestLogin),
//                     typeof(ReqPullingPlayerData),
//                     typeof(ResPullingPlayerData),
//                     typeof(ReqSelectPlayer),
//                     typeof(ResSelectPlayer),
//                     typeof(PlayerInfo),
//                     typeof(ReqRegisterUser),
//                     typeof(ResRegisterUser),
//                     typeof(ReqGuestUpgrade),
//                     typeof(ResGuestUpgrade),
//                     typeof(ReqRecvLoginBonues),
//                     typeof(ResRecvLoginBonues),
//                     typeof(ReqPullingBonuesList),
//                     typeof(ResPullingBonuesList),
//                     typeof(ReqRecvBonues),
//                     typeof(ResRecvBonues),
//                     typeof(ReqPullingTaskList),
//                     typeof(ResPullingTaskList),
//                     typeof(ReqTestGetGoods),
//                     typeof(ResTestGetGoods),
//                     typeof(ReqPullingMailMsg),
//                     typeof(ResPullingMailMsg),
//                     typeof(ReqUpdateMailMsg),
//                     typeof(ResUpdateMailMsg),
//                     typeof(ReqPullingRank),
//                     typeof(ResPullingRank),
//                     typeof(ReqPullingPackData),
//                     typeof(ResPullingPackData),
//                     typeof(ReqTestUpdateGooods),
//                     typeof(ResTestUpdateGoods),
//                     typeof(DicGoodsItem),
//                     typeof(ReqExchangeProduct),
//                     typeof(ResExchangeProduct),
//                     typeof(ReqEnterLogicServer),
//                     typeof(ResEnterLogicServer),
//                     typeof(ReqTestLogicCmdEcho),
//                     typeof(ResTestLogicCmdEcho),
//                     typeof(ReqExitLogicServer),
//                     typeof(ResExitLogicServer),
//                     typeof(ReqSitdown),
//                     typeof(ResSitdown),
//                     typeof(ReqStandup),
//                     typeof(ResStandup),
//                     typeof(ResEnterRoom),
//                     typeof(ResUserArrivedSeat),
//                     typeof(ResUserExitSeat),
//                     typeof(ReqTalkMsg),
//                     typeof(ResTalkMsg),
//                     typeof(ReqPlayerIsReady),
//                     typeof(ResPlayerIsReady),
//                     typeof(ResRoundReady),
//                     typeof(ResRoundStarted),
//                     typeof(ResRoundCheckOut),
//                     typeof(ResRoundClear),
//                     typeof(ResReconnRoom),
//                     typeof(ReqPlayerOpt),
//                     typeof(ResPlayerOpt),
//                     typeof(ResPlayerEscape),
//                     typeof(ReqPlayerSpawn),
//                     typeof(ResPlayerSpawn),
//                     typeof(ResEnterAOI),
//                     typeof(ResLeaveAOI),
//                     typeof(ReqNavToDst),
//                     typeof(ResNavToDst),
//                     typeof(ResSyncCharactorStatus),
//                     typeof(ReqStartSkill),
//                     typeof(ResStartSkill),
//                     typeof(ReqStartBuff),
//                     typeof(ResStartBuff),
//                     typeof(ResLostHp),
//                     typeof(ReqNavInDir),
//                     typeof(ResNavInDir),
//                     typeof(ResFrameSyncData),
//                     // ...
//                 },
//
//                 Package = "",
//                 Syntax = ProtoSyntax.Proto3,
//             };
//
//
//             string protoString = Serializer.GetProto(opt);
//             Console.WriteLine(protoString);
//             File.WriteAllText("../../../../../CocosClient/assets/Game/AssetsPackage/Protos/game.proto", protoString);
//         return 0;
//         
// #endif
    }
}
