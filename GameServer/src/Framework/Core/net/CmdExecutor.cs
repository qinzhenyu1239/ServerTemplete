using System;
using System.Reflection;

namespace Framework.Core.Net {

    public class CmdExecutor {
        /** logic controller  */
        public object handler;

        /** logic handler method */
        public MethodInfo method;

        /** arguments passed to method */
        public ParameterInfo[] @params;


        public static CmdExecutor Create(MethodInfo method, ParameterInfo[] @params, object handler) {
            CmdExecutor executor = new CmdExecutor();
            executor.method = method;
            executor.@params = @params;
            executor.handler = handler;

            return executor;
        }
    }

}

