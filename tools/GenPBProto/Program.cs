
using Game.Datas.Messages;
using ProtoBuf;
using ProtoBuf.Meta;

namespace GenPBProto {
    class Program
    {
        static void Main(string[] args) {
            var opt = new SchemaGenerationOptions
            {

                Types = {
                    typeof(ReqGuestLogin),
                    typeof(ReqUserLogin),
                    typeof(ResUserLogin),
                    typeof(AccountInfo),
                    typeof(ResGuestLogin),
                    typeof(ReqPullingPlayerData),
                    typeof(ResPullingPlayerData),
                    typeof(ReqSelectPlayer),
                    typeof(ResSelectPlayer),
                    typeof(PlayerInfo),
                    typeof(ReqRegisterUser),
                    typeof(ResRegisterUser),
                    typeof(ReqGuestUpgrade),
                    typeof(ResGuestUpgrade),
                    typeof(ReqRecvLoginBonues),
                    typeof(ResRecvLoginBonues),
                    typeof(ReqPullingBonuesList),
                    typeof(ResPullingBonuesList),
                    typeof(ReqRecvBonues),
                    typeof(ResRecvBonues),
                    typeof(ReqPullingTaskList),
                    typeof(ResPullingTaskList),
                    typeof(ReqTestGetGoods),
                    typeof(ResTestGetGoods),
                    typeof(ReqPullingMailMsg),
                    typeof(ResPullingMailMsg),
                    typeof(ReqUpdateMailMsg),
                    typeof(ResUpdateMailMsg),
                    typeof(ReqPullingRank),
                    typeof(ResPullingRank),
                    typeof(ReqPullingPackData),
                    typeof(ResPullingPackData),
                    typeof(ReqTestUpdateGooods),
                    typeof(ResTestUpdateGoods),
                    typeof(DicGoodsItem),
                    typeof(ReqExchangeProduct),
                    typeof(ResExchangeProduct),
                    typeof(ReqEnterLogicServer),
                    typeof(ResEnterLogicServer),
                    typeof(ReqTestLogicCmdEcho),
                    typeof(ResTestLogicCmdEcho),
                    typeof(ReqExitLogicServer),
                    typeof(ResExitLogicServer),
                    typeof(ReqSitdown),
                    typeof(ResSitdown),
                    typeof(ReqStandup),
                    typeof(ResStandup),
                    typeof(ResEnterRoom),
                    typeof(ResUserArrivedSeat),
                    typeof(ResUserExitSeat),
                    typeof(ReqTalkMsg),
                    typeof(ResTalkMsg),
                    typeof(ReqPlayerIsReady),
                    typeof(ResPlayerIsReady),
                    typeof(ResRoundReady),
                    typeof(ResRoundStarted),
                    typeof(ResRoundCheckOut),
                    typeof(ResRoundClear),
                    typeof(ResReconnRoom),
                    typeof(ReqPlayerOpt),
                    typeof(ResPlayerOpt),
                    typeof(ResPlayerEscape),
                    typeof(ReqPlayerSpawn),
                    typeof(ResPlayerSpawn),
                    typeof(ResEnterAOI),
                    typeof(ResLeaveAOI),
                    typeof(ReqNavToDst),
                    typeof(ResNavToDst),
                    typeof(ResSyncCharactorStatus),
                    typeof(ReqStartSkill),
                    typeof(ResStartSkill),
                    typeof(ReqStartBuff),
                    typeof(ResStartBuff),
                    typeof(ResLostHp),
                    typeof(ReqNavInDir),
                    typeof(ResNavInDir),
                    // ...
                },

                Package = "",
                Syntax = ProtoSyntax.Proto3,
            };


            string protoString = Serializer.GetProto(opt);
            Console.WriteLine(protoString);
            File.WriteAllText("../../../../../../CocosClient/assets/Game/AssetsPackage/Protos/game.proto", protoString);
        }
    }
}


