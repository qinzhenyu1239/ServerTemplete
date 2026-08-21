using SqlSugar;
using System;

namespace GenDbClass
{
    class Program
    {
        static void Main(string[] args)
        {
            // step1: 创建连接
            var db = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = "Database=auth_10001;Data Source=127.0.0.1;Port=3306;User Id=root;Password=123456;Charset=utf8", // 数据库连接字符串
                DbType = DbType.MySql, // 指定数据库类型
                IsAutoCloseConnection = true, // 是否自动关闭连接
                InitKeyType = InitKeyType.Attribute // 从实体特性中读取主键自增列信息
            });

            // 为了规范所有的命名，我们统一給关系数据对象，加一个前缀DB_
            var tables = db.DbMaintenance.GetTableInfoList();
            for (int i = 0; i < tables.Count; i++) {
                Console.WriteLine($"{tables[i].Name}");
                string input = tables[i].Name;
                string output = input.Substring(0, 1).ToUpper() + input.Substring(1);
                db.MappingTables.Add(output, tables[i].Name);
            }
            // end


            db.DbFirst.CreateClassFile("../../../../../GameServer/src/Game/Datas/DbEntities", "Game.Datas.DBEntities");

            // step1: 创建连接
            var gamedb = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = "Database=ugame_10001;Data Source=127.0.0.1;Port=3306;User Id=root;Password=123456;Charset=utf8", // 数据库连接字符串
                DbType = DbType.MySql, // 指定数据库类型
                IsAutoCloseConnection = true, // 是否自动关闭连接
                InitKeyType = InitKeyType.Attribute // 从实体特性中读取主键自增列信息
            });

            // 为了规范所有的命名，我们统一給关系数据对象，加一个前缀DB_
            var gameTables = gamedb.DbMaintenance.GetTableInfoList();
            for (int i = 0; i < gameTables.Count; i++)
            {
                Console.WriteLine($"{gameTables[i].Name}");
                string input = gameTables[i].Name;
                string output = input.Substring(0, 1).ToUpper() + input.Substring(1);
                gamedb.MappingTables.Add(output, gameTables[i].Name);
            }
            // end
            gamedb.DbFirst.CreateClassFile("../../../../../GameServer/src/Game/Datas/DbEntities", "Game.Datas.DBEntities");


        }
    }
}
