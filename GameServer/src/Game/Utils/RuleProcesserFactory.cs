using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Game.Utils
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public class RuleModule : Attribute
    {
        public int module;
        public int baseId;
        
        public RuleModule(int module, int baseId)
        {
            this.module = module;
            this.baseId = baseId;
        }
    }

    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public class RuleProcesser : Attribute
    {
        public int subType;
        public string funcName;

        public RuleProcesser(string name, int subType)
        {
            this.funcName = name;
            this.subType = subType;
        }
    }

    public class RuleProcesserFactory
    {
        /* 字典结构
         * Dic<int, Dic<int, Dic<string, Method>>>
         * [任务]====>[200001] ===> "Func1", Func1
         *                     ===> "Func2", Func2
         *                     ===> "Func3", Func3
         *             [200000 -1] ===> "Func1", Func1
         *      
         * [奖励]=====>[200001] ===> "Func1", Func1
         *                     ===> "Func2", Func2
         *                     ===> "Func3", Func3
         *             [200000 -1] ===> "Func1", Func1
         * ....
         */
        static Dictionary<int, Dictionary<int, Dictionary<string, MethodInfo>>> ruleSet; 
        public static void Init() {
            ruleSet = ScanRuleProcesser();
        }

        private static void ScaneOneRule(Dictionary<int, Dictionary<int, Dictionary<string, MethodInfo>>> ruleSet, Type t, RuleModule mainRule)
        {
            Dictionary<int, Dictionary<string, MethodInfo>> oneRuleSet = null;
            if (!ruleSet.ContainsKey(mainRule.module)) {
                oneRuleSet = new Dictionary<int, Dictionary<string, MethodInfo>>();
                ruleSet.Add(mainRule.module, oneRuleSet);
            }
            else {
                oneRuleSet = ruleSet[mainRule.module];
            } 

            MethodInfo[] funcs = t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int j = 0; j < funcs.Length; j++)
            {
                RuleProcesser p = funcs[j].GetCustomAttribute<RuleProcesser>();
                if (p == null)
                {
                    continue;
                }

                int subType = (p.subType == -1) ? -1 : (p.subType % mainRule.baseId);
                int key = mainRule.baseId + subType;

                Dictionary<string, MethodInfo> processFuncs = null;
                if (!oneRuleSet.ContainsKey(key))
                {
                    processFuncs = new Dictionary<string, MethodInfo>();
                    oneRuleSet.Add(key, processFuncs);
                }
                else { 
                    processFuncs = oneRuleSet[key];
                }

                processFuncs.Add(p.funcName, funcs[j]);
            }
        }

        public static Dictionary<int, Dictionary<int, Dictionary<string, MethodInfo>>> ScanRuleProcesser()
        {
            Dictionary<int, Dictionary<int, Dictionary<string, MethodInfo>>> ruleSet = new Dictionary<int, Dictionary<int, Dictionary<string, MethodInfo>>>();
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                Type[] allTypes = assembly.GetTypes();
                for (int i = 0; i < allTypes.Length; i++)
                {
                    Type t = allTypes[i];
                    RuleModule mainRule = t.GetCustomAttribute<RuleModule>();
                    if (mainRule != null)
                    {
                        ScaneOneRule(ruleSet, t, mainRule);
                    }
                }
            }
            return ruleSet;
        }

        private static MethodInfo GetProcesserFunc(string funcName, Dictionary<string, MethodInfo> funMap, Dictionary<string, MethodInfo> defaultFunMap)
        {
            if (funMap.ContainsKey(funcName))
            {
                return funMap[funcName];
            }
            if (defaultFunMap.ContainsKey(funcName))
            {
                return defaultFunMap[funcName];
            }

            return null;
        }

        public static MethodInfo GetProcesser(int module, string name, int tid, int baseId)
        {
            int key = baseId;
            key = key - 1;

            Dictionary<int, Dictionary<string, MethodInfo>> oneRuleSet = null;
            if (!ruleSet.ContainsKey(module)) {
                return null;
            }

            oneRuleSet = ruleSet[module];
            Dictionary<string, MethodInfo> funMap = null;
            Dictionary<string, MethodInfo> defaultFunMap = null;

            if (oneRuleSet.ContainsKey(key))
            {
                defaultFunMap = oneRuleSet[key];
            }

            if (oneRuleSet.ContainsKey(tid))
            {
                funMap = oneRuleSet[tid];
            }
            else
            {
                funMap = defaultFunMap;
            }
            if (funMap == null)
            {
                return null;
            }

            MethodInfo actionFunc = GetProcesserFunc(name, funMap, defaultFunMap);
            return actionFunc;
        }


    }
}
