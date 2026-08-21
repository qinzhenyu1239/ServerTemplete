using SqlSugar;
using System;
using System.IO;

namespace GenDbClass
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine(System.IO.Directory.GetCurrentDirectory());
            string directoryPath = System.IO.Directory.GetCurrentDirectory() + "./../../../../../GameServer/Configs/Csvs/";
            string outputPath = System.IO.Directory.GetCurrentDirectory() + "./../../../../../GameServer/src/Game/Datas/Excels/";
            string[] files = Directory.GetFiles(directoryPath, "*.*", SearchOption.AllDirectories);
            // 遍历所有文件
            foreach (string file in files)
            {
                Console.WriteLine(file);
                CreatConfigUitl.CreatConfigFile(file, outputPath);
            }
        }
    }
}
