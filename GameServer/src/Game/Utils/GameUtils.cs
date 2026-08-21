using ProtoBuf;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Policy;

namespace Game.Utils {
    public class GameUtils
    {
        public static int Random(int min, int max)
        {
            Random random = new Random();
            int s = random.Next(max) % (max - min + 1) + min;
            return s;
        }

        public static List<int> ParseStringWithIntValues(string formatString) {
            List<int> values = new List<int>();
            string[] conds = formatString.Split('|');

            for (int i = 0; i < conds.Length; i++) {
                values.Add(int.Parse(conds[i]));
            }
            return values;
        }

        public static Dictionary<string, int> ParseStringWithKeyIntValue(string formatString)
        {

            Dictionary<string, int> props = new Dictionary<string, int>();
            string[] conds = formatString.Split('|');
            for (int i = 0; i < conds.Length; i++)
            {
                string[] keyAndValue = conds[i].Split('=');
                string key = keyAndValue[0];
                int value = int.Parse(keyAndValue[1]);

                props.Add(key, value);
            }

            return props;
        }

        public static byte[] PbObjToBytes<T>(T objectData)
        {
            var stream = new MemoryStream();
            Serializer.Serialize<T>(stream, (T)objectData);
            byte[] data = stream.ToArray();
            return data;
        }

        public static T BytesToPbObj<T>(byte[] data)
        {
            var stream = new MemoryStream(data);
            return Serializer.Deserialize<T>(stream);
        }

        public static object GetFiled(object inst, string filedName, object defaultObj)
        {
            Type t = inst.GetType();
            FieldInfo f = t.GetField(filedName);
            if (f == null)
            {
                return defaultObj;
            }

            return f.GetValue(inst);
        }

        public static void SetFiled(object inst, string filedName, object value)
        {
            Type t = inst.GetType();
            FieldInfo f = t.GetField(filedName);
            if (f == null)
            {
                return;
            }

            f.SetValue(inst, value);
        }

        public static object GetProp(object inst, string propName, object defaultObj)
        {
            Type t = inst.GetType();
            PropertyInfo prop = t.GetProperty(propName);
            if (prop == null)
            {
                return defaultObj;
            }

            return prop.GetValue(inst);
        }

        public static void SetProp(object inst, string propName, object value)
        {
            Type t = inst.GetType();
            PropertyInfo prop = t.GetProperty(propName);
            if (prop == null)
            {
                return;
            }

            prop.SetValue(inst, value);
        }
    }
}
