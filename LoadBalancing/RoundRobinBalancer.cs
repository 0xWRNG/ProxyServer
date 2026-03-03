using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.LoadBalancing
{
    public class RoundRobinBalancer : ILoadBalancer
    {
        private readonly List<string> _servers;
        private int _index = 0;

        public RoundRobinBalancer(List<string> servers)
        {
            _servers = servers;
        }

        public string GetNextServer()
        {
            var server = _servers[_index];
            _index = (_index + 1) % _servers.Count;
            return server;
        }
    }
}
