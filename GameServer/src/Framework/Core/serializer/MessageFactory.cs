using Framework.Core.Utils;
using Microsoft.CSharp.RuntimeBinder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Core.Serializer
{
    class MessageFactory
    {
        public static MessageFactory Instance = new MessageFactory();

        private Dictionary<int, Type> keyTypeDic = new();
        // private Dictionary<Type, int> TypeKeyDic = new();

        private NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();
        public void InitMeesagePool() {
            List<Type> messages = TypeScanner.ListAllSubTypes(typeof(Message)).ToList();

            foreach (Type message in messages) {
                MessageMeta meta = message.GetCustomAttribute<MessageMeta>();
                if (meta == null) {
                    throw new RuntimeBinderException($"[致命错误]:没有找到[{message.Name}]的MessageMeta");
                }

                int key = BuildKey(meta.module, meta.cmd);
                if (keyTypeDic.ContainsKey(key)) {
                    throw new RuntimeBinderException($"[致命错误]:[{key}]重复注册");
                }

                keyTypeDic.Add(key, message);
                // TypeKeyDic.Add(message, key);
            }
        }

        public int BuildKey(short module, short cmd) {
            return module * (10000) + cmd;
        }

        public bool GetMessage(short module, short cmd, out Type msgType)
        {
            return keyTypeDic.TryGetValue(BuildKey(module, cmd), out msgType);
        }
        
    }
}
