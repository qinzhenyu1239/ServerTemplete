using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Framework.Core.Serializer
{
    public class SerializerHelper {
        private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

        static public byte[] PBEncoder(Message msg) {
            byte[] body = null;

            try {
                using (var stream = new MemoryStream()) {
                    ProtoBuf.Serializer.Serialize(stream, msg);
                    body = stream.ToArray();
                }
            }
            catch (IOException e) {
                logger.Error(e.Message);
            }
            return body;
        }

        static public Message PbDecode(short module, short cmd, byte[] body, int offset, int count) {

            MessageFactory.Instance.GetMessage(module, cmd, out Type msgType);

            if (body == null) {
                return (Message)Activator.CreateInstance(msgType);
            }

            try {
                using (var stream = new MemoryStream(body, offset, count)) {
                    Message _fw = (Message)ProtoBuf.Serializer.Deserialize(msgType, stream);
                    return _fw;
                }
            } catch (Exception e) {
                logger.Error($"读取消息出错,模块号[{module}]，类型[{cmd}],异常:{e.Message}");
            }

            return null;
        }
    }
}
