using ProxyServer.Filtering;
using ProxyServer.Monitoring;
using ProxyServer.Utils;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ProxyServer.Protocol
{
    public class HttpsTunnelHandler : IProtocolHandler
    {
        private readonly StatisticsCollector _stats;
        private readonly Logger _logger;
        private readonly IFilter? _filter;


        public HttpsTunnelHandler(IFilter? filter, StatisticsCollector stats, Logger logger)
        {
            _filter = filter;
            _stats = stats;
            _logger = logger;
        }

        public async Task HandleAsync(TcpClient client, string? rawRequest)
        {
            if (rawRequest == null)
                return;

            var request = new HttpRequest(rawRequest);
            if (_filter!=null && !_filter.IsAllowed(request.Host))
            {
                _logger.Log(LogLevels.Protocol, $"[FILTER]: Domain is not alowed: {request.Host}");
                await ResponseHelper.SendForbidden(client.GetStream());
                return;
            }
            _logger.Log(LogLevels.Protocol,"[CONNECT]: {0}:{1}", request.Host, request.Port);

            using var server = new TcpClient();
            await server.ConnectAsync(request.Host, request.Port);

            var clientStream = client.GetStream();
            var serverStream = server.GetStream();

            await ResponseHelper.SendConnectionEstablished(clientStream);
            _logger.Log(LogLevels.Transport,"Tunnel established {0}:{1}",request.Host,request.Port);
            TcpTunnel tcpTunnel = new TcpTunnel(_logger);
            await tcpTunnel.StartAsync(clientStream, serverStream, request.Host, request.Port);
        }
    }
}
