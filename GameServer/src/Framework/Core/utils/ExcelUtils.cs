using System.Collections;
using System.Collections.Generic;
using System;
using System.Reflection;
using System.IO;

/**
*   版权：博毅创为教育咨询有限公司  2018
*   作者：Felix老师
*   主键: key, ID, 如果没有用索引，从0开始
*/

namespace Framework.Core.Utils {

	public class ExcelUtils
	{

		private static NLog.Logger logger = NLog.LogManager.GetCurrentClassLogger();

		static Dictionary<string, Dictionary<string, object>> dataDic = new Dictionary<string, Dictionary<string, object>>();

		public static object GetConfigData(Type setT, string key, string fileName = null) {
            if (fileName == null)
            {
                fileName = setT.Name;
            }

            if (!dataDic.ContainsKey(fileName))
            {
                ReadConfigData(setT, fileName);
            }

            Dictionary<string, object> objDic = dataDic[fileName];
            // Debug.Log("test  (" + key + ")" + objDic.Count);
            if (!objDic.ContainsKey(key))
            {
                // throw new Exception("no this config");
                return null;
            }
            return (objDic[key]);
        }


        public static object GetConfigData<T>(string key, string fileName = null)
		{
			Type setT = typeof(T);
			if (fileName == null)
			{
				fileName = setT.Name;
			}

			if (!dataDic.ContainsKey(fileName))
			{
				ReadConfigData<T>(fileName);
			}

			Dictionary<string, object> objDic = dataDic[fileName];
			// Debug.Log("test  (" + key + ")" + objDic.Count);
			if (!objDic.ContainsKey(key))
			{
				// throw new Exception("no this config");
				return null;
			}
			return (objDic[key]);
		}

		public static List<T> GetConfigDatas<T>(string fileName, bool isPriKey = true)
		{
			List<T> returnList = new List<T>();
			Type setT = typeof(T);
			if (fileName == null)
			{
				fileName = setT.Name;
			}

			if (!dataDic.ContainsKey(fileName))
			{
				ReadConfigData<T>(fileName, isPriKey);
			}

			Dictionary<string, object> objDic = dataDic[fileName];
			foreach (KeyValuePair<string, object> kvp in objDic)
			{
				returnList.Add((T)(kvp.Value));
			}
			return returnList;
		}

        public static void ReadConfigData(Type t, string fileName = null, bool hasPriKey = true)
        {
            if (fileName == null)
            {
                fileName = t.Name;
            }

            string path = "Configs/Csvs/" + fileName + ".csv";
            string getString = File.ReadAllText(path);
            CsvReaderByString csr = new CsvReaderByString(getString);

            Dictionary<string, object> objDic = new Dictionary<string, object>();

            FieldInfo[] fis = new FieldInfo[csr.ColCount];
            for (int colNum = 1; colNum < csr.ColCount + 1; colNum++)
            {
                fis[colNum - 1] = t.GetField(csr[3, colNum]);
            }

            int index = 0;
            for (int rowNum = 4; rowNum < csr.RowCount + 1; rowNum++)
            {
                var configObj = Activator.CreateInstance(t);
                for (int i = 0; i < fis.Length; i++)
                {
                    string fieldValue = csr[rowNum, i + 1];
                    object setValue = new object();

                    switch (fis[i].FieldType.ToString())
                    {
                        case "System.Boolean":
                            setValue = bool.Parse(fieldValue);
                            break;
                        case "System.Int32":
                            setValue = int.Parse(fieldValue);
                            break;
                        case "System.Int64":
                            setValue = long.Parse(fieldValue);
                            break;
                        case "System.String":
                            setValue = fieldValue;
                            break;
                        case "System.Single":
                            try
                            {
                                setValue = float.Parse(fieldValue);
                            }
                            catch (System.Exception)
                            {
                                setValue = 0.0f;
                            }

                            break;
                        default:
                            logger.Error("error data type: " + fis[i].FieldType.ToString());
                            break;
                    }
                    fis[i].SetValue(configObj, setValue);
                    if (hasPriKey && (fis[i].Name == "key" || fis[i].Name == "ID" || fis[i].Name == "id"))
                    {
                        //只检测key和id的值，然后添加到objDic 中
                        objDic.Add(setValue.ToString(), configObj);
                    }
                }

                if (!hasPriKey)
                {
                    objDic.Add(index.ToString(), configObj);
                }
                index++;
            }
            dataDic.Add(fileName, objDic);    //可以作为参数
        }

        public static void ReadConfigData<T>(string fileName = null, bool hasPriKey = true)
		{
			if (fileName == null)
			{
				fileName = typeof(T).Name;
			}

			string path = "Configs/Csvs/" + fileName + ".csv";
			string getString = File.ReadAllText(path);
			CsvReaderByString csr = new CsvReaderByString(getString);

			Dictionary<string, object> objDic = new Dictionary<string, object>();

			FieldInfo[] fis = new FieldInfo[csr.ColCount];
			for (int colNum = 1; colNum < csr.ColCount + 1; colNum++)
			{
				fis[colNum - 1] = typeof(T).GetField(csr[3, colNum]);
			}

			int index = 0;
			for (int rowNum = 4; rowNum < csr.RowCount + 1; rowNum++)
			{
				T configObj = Activator.CreateInstance<T>();
				for (int i = 0; i < fis.Length; i++)
				{
					string fieldValue = csr[rowNum, i + 1];
					object setValue = new object();

					switch (fis[i].FieldType.ToString())
					{
						case "System.Boolean":
							setValue = bool.Parse(fieldValue);
							break;
						case "System.Int32":
							setValue = int.Parse(fieldValue);
							break;
						case "System.Int64":
							setValue = long.Parse(fieldValue);
							break;
						case "System.String":
							setValue = fieldValue;
							break;
						case "System.Single":
							try
							{
								setValue = float.Parse(fieldValue);
							}
							catch (System.Exception)
							{
								setValue = 0.0f;
							}

							break;
						default:
							logger.Error("error data type: " + fis[i].FieldType.ToString());
							break;
					}
					fis[i].SetValue(configObj, setValue);
					if (hasPriKey && (fis[i].Name == "key" || fis[i].Name == "ID" || fis[i].Name == "id"))
					{
						//只检测key和id的值，然后添加到objDic 中
						objDic.Add(setValue.ToString(), configObj);
					}
				}

				if (!hasPriKey)
				{
					objDic.Add(index.ToString(), configObj);
				}
				index++;
			}
			dataDic.Add(fileName, objDic);    //可以作为参数
		}
	}

}
