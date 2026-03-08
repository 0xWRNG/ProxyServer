using ProxyServer.Cache;
using ProxyServer.Core;
using ProxyServer.Filtering;
using ProxyServer.LoadBalancing;
using ProxyServer.Monitoring;
using ProxyServer.Protocol;
using System.Net;
using System.Net.Sockets;
using ProxyServer.Utils;
namespace ProxyServer.Core
{
    public class ProxyServer
    {
        private readonly int _port;
        private readonly ConnectionManager _connectionManager;
        private readonly StatisticsCollector _stats;
        private readonly Logger _logger;

        private readonly HttpHandler _httpHandler;
        private readonly HttpsTunnelHandler _httpsHandler;
        private readonly Socks5Handler _socksHandler;
        private CancellationToken _cancellationToken;
        private TcpListener? _listener;

        public ProxyServer(int port, bool useCache, bool useFilter, bool reverseProxy, List<string>? backends, FilterPipeline? filterPipeline, CancellationToken cancellationToken)
        {
            _port = port;
            _connectionManager = new ConnectionManager(100);
            _stats = new StatisticsCollector();
            _logger = new Logger("log");

            var cache = new LruCache(100);

            var balanser = reverseProxy ? new RoundRobinBalancer(backends ?? new List<string>()) : null;

            _httpHandler = new HttpHandler(
                cache: useCache ? cache : null,
                balancer: reverseProxy ? balanser : null,
                filter: useFilter ? filterPipeline : null,
                stats: _stats,
                logger: _logger
                );

            _httpsHandler = new HttpsTunnelHandler(
                filter: useFilter ? filterPipeline : null,
                balancer: reverseProxy ? balanser : null,
                stats: _stats,
                logger: _logger
                );
            _socksHandler = new Socks5Handler(_logger, _stats);
            _cancellationToken = cancellationToken;
        }



        public async Task StartAsync()
        {
            _listener = new TcpListener(IPAddress.Any, _port);

            _listener.Start();

            string line = string.Concat(Enumerable.Repeat("─", Console.WindowWidth));
            Console.WriteLine($"{line}\nProxy started at port {_port}\n{line}");
            while (!_cancellationToken.IsCancellationRequested)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync(_cancellationToken);
                    _ = HandleClientAsync(client);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevels.Connection, $"Error accepting client: {ex.Message}");
                }
            }
        }
        public void Stop()
        {
            _listener?.Stop();
            _logger.Log(LogLevels.Connection, "Proxy stopped.");
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            await _connectionManager.AcquireAsync();

            var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
            var clientIp = remoteEndPoint?.Address.ToString();
            var clientPort = remoteEndPoint?.Port.ToString();

            _logger.Log(LogLevels.Connection, $"CONNECT {clientIp}:{clientPort}");

            try
            {
                NetworkStream stream = client.GetStream();

                int firstByte = stream.ReadByte();
                if (firstByte == -1)
                    return;

                var pushbackStream = new PushbackStream(stream, (byte)firstByte);
                if (firstByte == 0x05)
                {
                    await _socksHandler.HandleAsync(client, firstByte.ToString());
                    return;
                }
                while (true)
                {
                    string request = await RequestReader.ReadAsync(pushbackStream);

                    if (string.IsNullOrWhiteSpace(request))
                        break;

                    if (IsHttps(request))
                    {
                        await _httpsHandler.HandleAsync(client, request);
                        break;
                    }
                    else if (IsHttp(request))
                    {
                        await _httpHandler.HandleAsync(client, request);
                    }
                    else
                    {
                        _logger.Log(LogLevels.Transport,
                            $"Unknown protocol from {clientIp}:{clientPort}");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevels.Transport,
                    $"Error {clientIp ?? "X"}:{clientPort ?? "X"} - {ex.Message}");
            }
            finally
            {
                _logger.Log(LogLevels.Connection, $"DISCONNECT {clientIp}:{clientPort}");
                client.Close();
                _connectionManager.Release();
            }
        }
        private bool IsHttps(string request)
        {
            return request.StartsWith("CONNECT");
        }
        private bool IsHttp(string request)
        {
            return request.StartsWith("GET") ||
                   request.StartsWith("POST");
        }
    }

}
