

using System;
using System.IO;

/**
 *   版权：博毅创为教育咨询有限公司  2018
 *   作者：Felix老师
 */
public class CreatConfigUitl {
    public static void CreatConfigFile(string filePath, string writePath)
    {
        
        string className = filePath.Substring(filePath.LastIndexOf("/") + 1);
        className = className.Substring(0, className.IndexOf("."));
        StreamWriter sw = new StreamWriter(writePath + className + ".cs");

        sw.WriteLine("using System;\n");
        sw.WriteLine("using System.Collections;\n");
        sw.WriteLine("namespace Game.Datas.Excels {\n");
        sw.WriteLine("\t[Serializable]");
        sw.WriteLine("\tpublic class " + className);
        sw.WriteLine("\t{");

        CsvStreamReader csr = new CsvStreamReader(filePath);
        for (int colNum = 1; colNum < csr.ColCount + 1; colNum++)
        {
            string fieldName = csr[3, colNum];
            string fieldType = csr[2, colNum];
            sw.WriteLine("\t\t" + "public " + fieldType + " " + fieldName + ";" + "");
        }

        sw.WriteLine("\t}");
        sw.WriteLine("}");
        sw.Flush();
        sw.Close();
    }
}
