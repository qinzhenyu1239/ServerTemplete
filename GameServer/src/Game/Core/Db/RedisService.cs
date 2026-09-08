using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Game.Core.Db
{
    public class RedisService
    {
        public static RedisService Instance = new RedisService();

        private ConnectionStringSettings redisIpAddr;
        private ConnectionStringSettings redisPort;

        private readonly object connectionLock = new object();
        private ConnectionMultiplexer connection;


        public void Init() {
            this.redisIpAddr = ConfigurationManager.ConnectionStrings["redisIpAddr"];
            this.redisPort = ConfigurationManager.ConnectionStrings["redisPort"];
        }

        private ConnectionMultiplexer GetConnection()
        {
            if (connection != null && connection.IsConnected)
            {
                return connection;
            }

            lock (connectionLock)
            {
                if (connection != null && connection.IsConnected)
                {
                    return connection;
                }

                if (redisIpAddr == null || redisPort == null)
                {
                    Init();
                }

                connection?.Dispose();
                connection = ConnectionMultiplexer.Connect(new ConfigurationOptions
                {
                    EndPoints = { { redisIpAddr.ConnectionString, int.Parse(redisPort.ConnectionString) } },
                    AbortOnConnectFail = false,
                    ConnectRetry = 3
                });
                return connection;
            }
        }

        public IDatabase GetAuthDatabase()
        {
            return GetConnection().GetDatabase(4);
        }

        public ISubscriber GetSubscriber()
        {
            return GetConnection().GetSubscriber();
        }

        public void Shutdown()
        {
            lock (connectionLock)
            {
                connection?.Close();
                connection?.Dispose();
                connection = null;
            }
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
