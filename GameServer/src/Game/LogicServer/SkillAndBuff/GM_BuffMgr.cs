using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace Game.LogicServer {
    public class GM_BuffMgr
    {
        public static GM_BuffMgr Instance = new GM_BuffMgr();
        private Dictionary<int, Dictionary<string, MethodInfo>> buffModelSet = null;
        private List<Type> allBuffConfigType = null;

        public void Init()
        {
            this.buffModelSet = new Dictionary<int, Dictionary<string, MethodInfo>>();
            this.allBuffConfigType = new List<Type>();
            this.ScanAllSkillModelAndConfig();
        }

        private void ScaneOneBuffModel(Type t, BuffModel buffModel)
        {
            MethodInfo[] funcs = t.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            for (int j = 0; j < funcs.Length; j++)
            {
                BuffProcesser p = funcs[j].GetCustomAttribute<BuffProcesser>();
                if (p == null)
                {
                    continue;
                }

                // Debug.Log(p.funcName);
                int key = buffModel.mainType + p.subType;

                Dictionary<string, MethodInfo> processFuncs = null;

                if (!this.buffModelSet.ContainsKey(key))
                {
                    processFuncs = new Dictionary<string, MethodInfo>();
                    this.buffModelSet.Add(key, processFuncs);
                }
                else
                {
                    processFuncs = this.buffModelSet[key];
                }
                processFuncs.Add(p.propName, funcs[j]);
            }
        }

        private void ScaneOneBuffConfig(Type t, BuffConfig buffConfig)
        {
            this.allBuffConfigType.Add(t);
        }

        private void ScanAllSkillModelAndConfig()
        {
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (Assembly assembly in assemblies)
            {
                Type[] allTypes = assembly.GetTypes();
                for (int i = 0; i < allTypes.Length; i++)
                {
                    Type t = allTypes[i];
                    BuffModel buffModel = t.GetCustomAttribute<BuffModel>();
                    if (buffModel != null)
                    {
                        this.ScaneOneBuffModel(t, buffModel);
                    }

                    BuffConfig buffConfig = t.GetCustomAttribute<BuffConfig>();
                    if (buffConfig != null)
                    {
                        this.ScaneOneBuffConfig(t, buffConfig);
                    }
                }
            }
        }

        private MethodInfo GetProcesserFunc(string funcName, Dictionary<string, MethodInfo> funMap, Dictionary<string, MethodInfo> defaultFunMap)
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

        private MethodInfo GetProcesserFuncByBuffId(int buffId, string propName)
        {
            int mainType = (int)(buffId / 100000);
            int subType = buffId % 100000;

            int key = mainType * 100000;
            Dictionary<string, MethodInfo> funMap = null;
            Dictionary<string, MethodInfo> defaultFunMap = null;

            key = key - 1;
            if (this.buffModelSet.ContainsKey(key))
            {
                defaultFunMap = this.buffModelSet[key];
            }


            if (this.buffModelSet.ContainsKey(buffId))
            {
                funMap = this.buffModelSet[buffId];
            }
            else
            {
                funMap = defaultFunMap;
            }

            MethodInfo m = this.GetProcesserFunc(propName, funMap, defaultFunMap);

            return m;
        }

        public BuffNode CreateBuffNode(int buffId)
        {

            MethodInfo m = this.GetProcesserFuncByBuffId(buffId, "BuffTime");
            BuffNode node = new BuffNode(buffId);
            if (m != null)
            {
                m.Invoke(null, new object[] { node });
            }


            return node;
        }

        public void CalcFightBuffWithProp(int buffId, string propName, FightCalcResult ret)
        {
            MethodInfo m = this.GetProcesserFuncByBuffId(buffId, propName);
            if (m == null)
            {
                return;
            }

            object[] paramsData = new object[] { buffId, ret };
            m.Invoke(null, paramsData);
        }
    }

}
