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
    public class HttpResponse
    {
        public int StatusCode { get; set; }
        public string ReasonPhrase { get; set; } = "";
        public byte[] Body { get; set; } = Array.Empty<byte>();
        public string Version { get; set; } = "";
        public Dictionary<string, string> Headers { get; } = new(StringComparer.OrdinalIgnoreCase);

        public bool IsChunked =>
        Headers.TryGetValue("Transfer-Encoding", out var value) &&
        value.Contains("chunked", StringComparison.OrdinalIgnoreCase);

        public long? ContentLength =>
            Headers.TryGetValue("Content-Length", out var value) &&
            long.TryParse(value, out var len) ? len : null;
        public byte[] ToByteArray()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"{Version} {StatusCode} {ReasonPhrase}");

            foreach (var header in Headers)
                sb.AppendLine($"{header.Key}: {header.Value}");

            sb.AppendLine();
            var headerBytes = Encoding.UTF8.GetBytes(sb.ToString());
            return headerBytes.Concat(Body).ToArray();
        }

        public static async Task<HttpResponse> ReadAsync(NetworkStream stream)
        {
            var response = new HttpResponse();
            string headersText = await ReadHeadersAsync(stream);

            var headerLines = headersText.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

            var statusLineParts = headerLines[0].Split(' ', 3);

            response.Version = statusLineParts[0];
            response.StatusCode = int.Parse(statusLineParts[1]);
            response.ReasonPhrase = statusLineParts.Length > 2 ? statusLineParts[2] : "";

            foreach (var line in headerLines.Skip(1))
            {
                int idx = line.IndexOf(':');
                if (idx <= 0) continue;
                var key = line[..idx].Trim();
                var val = line[(idx + 1)..].Trim();
                response.Headers[key] = val;
            }

            response.Body = await ReadBodyAsync(stream, response);
            return response;
        }
        private static async Task<string> ReadHeadersAsync(NetworkStream stream)
        {
            var buffer = new List<byte>();
            var temp = new byte[1];

            while (true)
            {
                int read = await stream.ReadAsync(temp, 0, 1);
                if (read == 0)
                    break;

                buffer.Add(temp[0]);

                int count = buffer.Count;
                if (count >= 4 &&
                    buffer[count - 4] == '\r' &&
                    buffer[count - 3] == '\n' &&
                    buffer[count - 2] == '\r' &&
                    buffer[count - 1] == '\n')
                {
                    break;
                }
            }

            return Encoding.UTF8.GetString(buffer.ToArray());
        }
        private static async Task<byte[]> ReadBodyAsync(NetworkStream stream, HttpResponse response)
        {
            var ms = new MemoryStream();
            var buffer = new byte[8192];

            if (response.IsChunked)
            {
                while (true)
                {
                    string chunkSizeLine = await ReadLineAsync(stream);
                    int chunkSize = int.Parse(chunkSizeLine, System.Globalization.NumberStyles.HexNumber);

                    if (chunkSize == 0)
                    {
                        await ReadLineAsync(stream);
                        break;
                    }

                    int remaining = chunkSize;

                    while (remaining > 0)
                    {
                        int read = await stream.ReadAsync(
                            buffer, 0, Math.Min(buffer.Length, remaining));

                        if (read == 0)
                            break;

                        ms.Write(buffer, 0, read);
                        remaining -= read;
                    }

                    await ReadLineAsync(stream);
                }
            }
            else if (response.ContentLength.HasValue)
            {
                long remaining = response.ContentLength.Value;

                while (remaining > 0)
                {
                    int read = await stream.ReadAsync(
                        buffer, 0, (int)Math.Min(buffer.Length, remaining));

                    if (read == 0)
                        break;

                    ms.Write(buffer, 0, read);
                    remaining -= read;
                }
            }
            else
            {
                int read;
                while ((read = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    ms.Write(buffer, 0, read);
            }

            return ms.ToArray();
        }
        private static async Task<string> ReadLineAsync(NetworkStream stream)
        {
            var bytes = new List<byte>();
            var buffer = new byte[1];

            while (true)
            {
                int read = await stream.ReadAsync(buffer, 0, 1);
                if (read == 0)
                    break;

                bytes.Add(buffer[0]);

                int count = bytes.Count;
                if (count >= 2 &&
                    bytes[count - 2] == '\r' &&
                    bytes[count - 1] == '\n')
                    break;
            }

            return Encoding.UTF8
                .GetString(bytes.ToArray())
                .TrimEnd('\r', '\n');
        }
    }
    public class HttpHandler : IProtocolHandler
    {

        private readonly IFilter? _filter;
        private readonly ICache? _cache;
        private readonly ILoadBalancer? _balancer;

        private readonly StatisticsCollector _stats;
        private readonly Logger _logger;



        public HttpHandler(ICache? cache, ILoadBalancer? balancer, IFilter? filter,
                   StatisticsCollector stats, Logger logger)
        {
            _cache = cache;
            _balancer = balancer;
            _filter = filter;
            _stats = stats;
            _logger = logger;
        }

        public async Task HandleAsync(TcpClient client, string? rawRequest)
        {
            if (rawRequest == null)
                return;

            var request = new HttpRequest(rawRequest ?? "");
            _logger.Log(LogLevels.Protocol, $"[HTTP]: {request.Method} {request.Host}:{request.Port}{request.Path}");

            if (_filter!=null && !_filter.IsAllowed(request.Host))
            {
                _logger.Log(LogLevels.Protocol, $"[FILTER]: Domain is not alowed:{request.Host}");
                await ResponseHelper.SendForbidden(client.GetStream());
                return;
            }

            string key = ICache.GetCacheKey(request.Host, request.Port, request.Path);
            if (_cache != null && _cache.Get(key, out var cached))
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

            var requestBytes = Encoding.UTF8.GetBytes(request.ToRawRequest());
            await serverStream.WriteAsync(requestBytes);

            var response = await HttpResponse.ReadAsync(serverStream);
            if (response.IsChunked)
            {
                response.Headers.Remove("Transfer-Encoding");
                response.Headers["Content-Length"] = response.Body.Length.ToString();
            }
            var responseBytes = response.ToByteArray();
            await clientStream.WriteAsync(responseBytes);

            if (_cache != null && response.StatusCode == 200)
            {
                string key = ICache.GetCacheKey(request.Host, request.Port, request.Path);

                _cache.Save(key, responseBytes);
                _logger.Log(LogLevels.Protocol, $"[CACHE]: Saved {request.Path} ({responseBytes.Length} bytes)");
            }
        }
        private Task<byte[]> ForwardToBackend(string backend, string request)
        {
            return Task.FromResult(new byte[0]);
        }
    }
}
