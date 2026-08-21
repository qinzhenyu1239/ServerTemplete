using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.Db
{
    class RedisService
    {
        public static RedisService Instance = new RedisService();

        private ConnectionStringSettings redisIpAddr;
        private ConnectionStringSettings redisPort;


        public void Init() {
            this.redisIpAddr = ConfigurationManager.ConnectionStrings["redisIpAddr"];
            this.redisPort = ConfigurationManager.ConnectionStrings["redisPort"];
        }


        public void SortedSetAdd(string name, string key, int value) {
            try {
                ConfigurationOptions configurationOptions = new ConfigurationOptions
                {
                    EndPoints = { { this.redisIpAddr.ConnectionString, int.Parse(this.redisPort.ConnectionString) } },
                };
                ConnectionMultiplexer connection = ConnectionMultiplexer.Connect(configurationOptions);

                IDatabase database = connection.GetDatabase(3);
                database.SortedSetAdd(name, key, value);

                connection.Close();
                connection.Dispose();
            } catch (Exception e) { 

            }
            
        }


        public SortedSetEntry[] SortedSetRangeByRankWithScores(string rankName, int num) {
            try {
                ConfigurationOptions configurationOptions = new ConfigurationOptions
                {
                    EndPoints = { { this.redisIpAddr.ConnectionString, int.Parse(this.redisPort.ConnectionString) } },
                };
                ConnectionMultiplexer connection = ConnectionMultiplexer.Connect(configurationOptions);

                IDatabase database = connection.GetDatabase(3);
                SortedSetEntry[] rankValueWithScore = database.SortedSetRangeByRankWithScores(rankName, 0, num, Order.Descending);

                connection.Close();
                connection.Dispose();

                return rankValueWithScore;

            } catch (Exception) { 
            }

            return null;
        }
    }
}
