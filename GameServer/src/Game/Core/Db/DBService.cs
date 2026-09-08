using SqlSugar;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.Db
{
    class DBService {
        public static DBService Instance = new DBService();

        private ConnectionStringSettings connAuthUserStr;
        private ConnectionStringSettings connGameDataStr;
        private int commandTimeoutSeconds = 5;


        public void Init() {
            connAuthUserStr = ConfigurationManager.ConnectionStrings["connAuthUserStr"];
            connGameDataStr = ConfigurationManager.ConnectionStrings["connGameDataStr"];
            if (int.TryParse(ConfigurationManager.AppSettings["DatabaseCommandTimeoutSeconds"], out int configuredTimeout) &&
                configuredTimeout > 0)
            {
                commandTimeoutSeconds = configuredTimeout;
            }
        }

        public SqlSugarClient GetGameInstance() {
            //创建数据库对象
            SqlSugarClient db = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = connGameDataStr.ConnectionString,
                DbType = DbType.MySql,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute//从特性读取主键自增信息
            }); ;


            //添加Sql打印事件，开发中可以删掉这个代码
            db.Aop.OnLogExecuting = (sql, pars) =>
            {
                //Console.WriteLine(sql);
            };
            db.Ado.CommandTimeOut = commandTimeoutSeconds;
            return db;
        }

        public SqlSugarClient GetAuthInstance() {
            //创建数据库对象
            SqlSugarClient db = new SqlSugarClient(new ConnectionConfig()
            {
                ConnectionString = connAuthUserStr.ConnectionString,
                DbType = DbType.MySql,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute//从特性读取主键自增信息
            }); ;


            //添加Sql打印事件，开发中可以删掉这个代码
            db.Aop.OnLogExecuting = (sql, pars) =>
            {
                //Console.WriteLine(sql);
            };
            db.Ado.CommandTimeOut = commandTimeoutSeconds;
            return db;

        }
    }
}


