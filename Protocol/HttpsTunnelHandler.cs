using ProxyServer.Monitoring;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ProxyServer.Protocol
{
    public class HttpsTunnelHandler : IProtocolHandler
    {
        private readonly StatisticsCollector _stats;

        public HttpsTunnelHandler(StatisticsCollector stats)
        {
            _stats = stats;
        }

        public async Task HandleAsync(TcpClient client, string? request)
        {
            _stats.IncrementRequests();
        }
    }
}
