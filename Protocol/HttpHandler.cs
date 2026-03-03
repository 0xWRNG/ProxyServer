using ProxyServer.Cache;
using ProxyServer.Filtering;
using ProxyServer.LoadBalancing;
using ProxyServer.Monitoring;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ProxyServer.Protocol
{
    public class HttpHandler : IProtocolHandler
    {
        private readonly ICache _cache;
        private readonly IFilter _filter;
        private readonly ILoadBalancer _balancer;
        private readonly StatisticsCollector _stats;

        public HttpHandler(ICache cache, IFilter filter,
                           ILoadBalancer balancer,
                           StatisticsCollector stats)
        {
            _cache = cache;
            _filter = filter;
            _balancer = balancer;
            _stats = stats;
        }

        public async Task HandleAsync(TcpClient client, string? request)
        {
            if (!_filter.IsAllowed(request))
            {
                await ResponseHelper.SendForbidden(client);
                return;
            }

            if (_cache.Get(request, out var cached))
            {
                await ResponseHelper.Send(client, cached);
                return;
            }

            string backend = _balancer.GetNextServer();
            byte[] response = await ForwardToBackend(backend, request);

            _cache.Save(request, response);
            await ResponseHelper.Send(client, response);

            _stats.IncrementRequests();
        }

        private Task<byte[]> ForwardToBackend(string backend, string request)
        {
            return Task.FromResult(new byte[0]);
        }
    }
}
