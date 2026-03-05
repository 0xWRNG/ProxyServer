using ProxyServer.Filtering;
using ProxyServer.LoadBalancing;
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
        private readonly ILoadBalancer? _balancer;
        private readonly IFilter? _filter;


        public HttpsTunnelHandler(IFilter? filter, ILoadBalancer? balancer, StatisticsCollector stats, Logger logger)
        {
            _filter = filter;
            _balancer = balancer;
            _stats = stats;
            _logger = logger;
        }

        public async Task HandleAsync(TcpClient client, string? rawRequest)
        {
            if (rawRequest == null)
                return;
            var request = new HttpRequest(rawRequest);
            string targetHost = request.Host;
            int targetPort = request.Port;

            if (_balancer != null)
            {
                var backend = _balancer.GetNextServer();
                targetHost = backend.Host;
                targetPort = backend.Port;
                _logger.Log(LogLevels.Protocol, $"[REVERSE HTTPS]: Forwarding CONNECT {request.Host}:{request.Port} -> {targetHost}:{targetPort}");
            }

            if (_filter!=null && !_filter.IsAllowed(request))
            {
                _logger.Log(LogLevels.Protocol, $"[FILTER]: {_filter.Reason}");
                await ResponseHelper.SendForbidden(client.GetStream());
                return;
            }
            _logger.Log(LogLevels.Protocol,"[CONNECT]: {0}:{1}", request.Host, request.Port);

            using var server = new TcpClient();
            await server.ConnectAsync(targetHost, targetPort);

            var clientStream = client.GetStream();
            var serverStream = server.GetStream();

            await ResponseHelper.SendConnectionEstablished(clientStream);
            _logger.Log(LogLevels.Transport,"Tunnel established {0}:{1}",request.Host,request.Port);
            TcpTunnel tcpTunnel = new TcpTunnel(_logger);
            await tcpTunnel.StartAsync(clientStream, serverStream, request.Host, request.Port);
        }
    }
}
