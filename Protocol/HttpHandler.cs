using ProxyServer.Cache;
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
    public class HttpRequest
    {
        public string Method { get; set; }
        public string Path { get; set; }
        public string Version { get; set; }
        public string Host { get; set; }
        public int Port { get; set; } = 80;
        public Dictionary<string, string> Headers { get; set; }

        public HttpRequest(string rawRequest)
        {
            var lines = rawRequest.Split("\r\n");
            var requestLine = lines[0].Split(' ');

            Method = requestLine[0];
            Path = requestLine[1];
            Version = requestLine[2];
            Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (Method.Equals("CONNECT", StringComparison.OrdinalIgnoreCase))
            {
                var parts = Path.Split(':');

                Host = parts[0];
                Port = parts.Length > 1 ? int.Parse(parts[1]) : 443;

                return;
            }

            foreach (var line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    break;

                int index = line.IndexOf(':');
                if (index <= 0)
                    continue;

                var key = line[..index].Trim();
                var value = line[(index + 1)..].Trim();

                Headers[key] = value;
            }

            if (Uri.TryCreate(Path, UriKind.Absolute, out var uri))
            {
                Host = uri.Host;
                Port = uri.Port;
                Path = uri.PathAndQuery;
                return;
            }
            if (Headers.TryGetValue("Host", out var hostHeader))
            {
                if (hostHeader.Contains(':'))
                {
                    var parts = hostHeader.Split(':');
                    Host = parts[0];
                    Port = int.Parse(parts[1]);
                }
                else
                {
                    Host = hostHeader;
                    Port = 80;
                }
            }
        }
        public string ToRawRequest()
        {
            var sb = new StringBuilder();

            sb.AppendLine($"{Method} {Path} {Version}");

            foreach (var header in Headers)
            {
                sb.AppendLine($"{header.Key}: {header.Value}");
            }

            sb.AppendLine();

            return sb.ToString();
        }
    }
    public class HttpHandler : IProtocolHandler
    {
        private readonly ICache _cache;
        private readonly IFilter _filter;
        private readonly ILoadBalancer _balancer;
        private readonly StatisticsCollector _stats;
        private readonly  Logger _logger;

        public HttpHandler(ICache cache, IFilter filter,
                           ILoadBalancer balancer,
                           StatisticsCollector stats, Logger logger)
        {
            _cache = cache;
            _filter = filter;
            _balancer = balancer;
            _stats = stats;
            _logger = logger;
        }

        public async Task HandleAsync(TcpClient client, string? rawRequest)
        {
            if (rawRequest == null)
                return;

            var request = new HttpRequest(rawRequest ?? "");
            _logger.Log(LogLevels.Protocol, $"[HTTP]: {request.Method} {request.Host}:{request.Port}{request.Path}");

            if (!_filter.IsAllowed(request.Host))
            {
                _logger.Log(LogLevels.Protocol, $"[FILTER]: Domain is not alowed:{request.Host}");
                await ResponseHelper.SendForbidden(client.GetStream());
                return;
            }

            string key = ICache.GetCacheKey(request.Host, request.Port, request.Path);
            if (_cache.Get(key, out var cached))
            {
                _logger.Log(LogLevels.Protocol, $"[CACHE]: Hit {request.Path} ({cached.Length} bytes)");
                await ResponseHelper.SendAsync(client.GetStream(), cached);
                return;
            }
            try
            {
                await ForwardToTarget(client.GetStream(), request);
                _stats.IncrementRequests();
            }
            catch
            {
                _logger.Log(LogLevels.Transport, "[ERROR]: Backend timeout");
                await ResponseHelper.SendBadGateway(client.GetStream());
            }
        }
        private async Task ForwardToTarget(NetworkStream clientStream, HttpRequest request)
        {
            using var server = new TcpClient();
            await server.ConnectAsync(request.Host, request.Port);
            var serverStream = server.GetStream();

            request.Headers["Connection"] = "close";
            string rawRequest = request.ToRawRequest();
            byte[] requestBytes = Encoding.UTF8.GetBytes(rawRequest);
            await serverStream.WriteAsync(requestBytes, 0, requestBytes.Length);

            using var ms = new MemoryStream();
            byte[] buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await serverStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await clientStream.WriteAsync(buffer, 0, bytesRead);
                ms.Write(buffer, 0, bytesRead);
            }

             byte[] responseData = ms.ToArray();
            string key = ICache.GetCacheKey(request.Host, request.Port, request.Path);
            _cache.Save(key, responseData);
            _logger.Log(LogLevels.Protocol, $"[CACHE]: Saved {request.Path} ({responseData.Length} bytes)");
        }
        private Task<byte[]> ForwardToBackend(string backend, string request)
        {
            return Task.FromResult(new byte[0]);
        }
    }
}
