using System;

namespace Framework.Core.Utils {

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class Controller : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RequestMapping : Attribute
    {
    }


    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class MessageMeta : Attribute
    {
        public short module;
        public short cmd;

        public MessageMeta(short module, short cmd) {
            this.module = module;
            this.cmd = cmd;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class HttpController : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class HttpRequestMapping : Attribute
    {
        public string uri;

        public HttpRequestMapping(string uri) {
            this.uri = uri;
        }
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class LogicServerMeta : Attribute {
        public int stype;
        public LogicServerMeta(int stype) {
            this.stype = stype;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class LogicMessageProc : Attribute
    {
        public short module;
        public short cmd;

        public LogicMessageProc(short module, short cmd)
        {
            this.module = module;
            this.cmd = cmd;
        }
    }

}

