using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.LoadBalancing
{

    public class RoundRobinBalancer : ILoadBalancer
    {
        
        private readonly List<Server> _servers;
        private int _index = 0;

        public RoundRobinBalancer(List<string> servers)
        {
            _servers = new List<Server>();
            foreach (string line in servers)
            {
                var parts = line.Split(":");
                _servers.Add(new Server(parts[0], int.Parse(parts[1])));

            }
        }
           

        public Server GetNextServer()
        {
            var server = _servers[_index];
            _index = (_index + 1) % _servers.Count;
            return server;
        }
    }
}
