using ProxyServer.Monitoring;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ProxyServer.Protocol
{
    public class Socks5Handler : IProtocolHandler
    {
        private readonly StatisticsCollector _stats;

        public Socks5Handler(StatisticsCollector stats)
        {
            _stats = stats;
        }

        public async Task HandleAsync(TcpClient client, string? request = null)
        {
            _stats.IncrementRequests();
        }
    }
}
