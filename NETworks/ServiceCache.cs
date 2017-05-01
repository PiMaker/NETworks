// File: ServiceCache.cs
// Created: 27.04.2017
// 
// See <summary> tags for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SimpleLogger;

namespace NETworks
{
    internal class ServiceCache
    {
        private const int TIMEOUT = 100;
        private const int TIMEOUT_TRIES = 50;

        private readonly Dictionary<string, List<Connection>> cache;
        private readonly object cacheLock = new object();
        private readonly NetworkManager networkManager;

        public ServiceCache()
        {
            this.cache = new Dictionary<string, List<Connection>>();

            this.networkManager = new NetworkManager();
            this.networkManager.RegisterClientListener(this.ServerConnected);
        }

        private void ServerConnected(string service, Connection connection)
        {
            Logger.Log("Incoming Server-Connection: " + connection);
            lock (this.cacheLock)
            {
                if (!this.cache.ContainsKey(service))
                {
                    this.cache.Add(service, new List<Connection>());
                }
                else
                {
                    if (this.cache[service].Any(x => x.Guid.Equals(connection.Guid)))
                    {
                        Logger.Log("Cache Hit on Guid, not adding");
                        return;
                    }
                }

                this.cache[service].Add(connection);
                Logger.Log("Service endpoint added");
            }
        }

        public async Task<Connection> Get(string service, bool allowDiscovery = true)
        {
            Logger.Log($"Service requested from Cache: {service}");

            Connection con;

            lock (this.cacheLock)
            {
                if (!this.cache.ContainsKey(service))
                {
                    this.cache.Add(service, new List<Connection>());
                }

                this.cache[service] = this.cache[service].Where(x => x.State != ConnectionState.Closed).ToList();
                con = this.cache[service].FirstOrDefault();
            }

            if (con == default(Connection))
            {
                if (allowDiscovery)
                {
                    Logger.Log("Cache miss, requesting discovery");
                    await this.networkManager.Discover(service);

                    Logger.Log("Commencing delay");

                    for (var i = 0; i < ServiceCache.TIMEOUT_TRIES; i++)
                    {
                        await Task.Delay(ServiceCache.TIMEOUT);

                        if (Network.Alive)
                        {
                            var newCon = await this.Get(service, false);
                            if (newCon != default(Connection))
                            {
                                return newCon;
                            }
                        }
                    }
                }

                Logger.Log("Cache miss, no discovery");
                return null;
            }

            return con;
        }

        public void Shutdown()
        {
            this.networkManager.Shutdown();

            lock (this.cacheLock)
            {
                foreach (var cacheValue in this.cache.Values)
                {
                    foreach (var connection in cacheValue)
                    {
                        connection.Close();
                    }
                }
            }
        }
    }
}