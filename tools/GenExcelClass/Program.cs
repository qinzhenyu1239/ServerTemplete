using System;
using System.IO;
using System.Linq;

namespace GenDbClass
{
    class Program
    {
        static void Main(string[] args)
        {
            string rootPath = Path.GetFullPath(
                Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            string directoryPath = Path.Combine(rootPath, "GameServer", "Configs", "Csvs");
            string outputPath = Path.Combine(rootPath, "GameServer", "src", "Game", "Datas", "Excels");

            Directory.CreateDirectory(outputPath);

            string[] files = Directory
                .GetFiles(directoryPath, "*.csv", SearchOption.AllDirectories)
                .OrderBy(file => file, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            Console.WriteLine($"CSV目录: {directoryPath}");
            Console.WriteLine($"输出目录: {outputPath}");

            foreach (string file in files)
            {
                Console.WriteLine(file);
                CreatConfigUitl.CreatConfigFile(file, outputPath);
            }
        }
    }
}
