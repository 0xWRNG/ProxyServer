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

        private readonly HttpHandler _httpHandler;
        private readonly HttpsTunnelHandler _httpsHandler;
        private readonly Socks5Handler _socksHandler;

        public ProxyServer(int port)
        {
            _port = port;
            _connectionManager = new ConnectionManager(100);
            _stats = new StatisticsCollector();

            var cache = new LruCache(100);
            var filter = new DomainFilter();
            var balancer = new RoundRobinBalancer(new List<string>
        {
            "http://localhost:5001",
            "http://localhost:5002"
        });

            _httpHandler = new HttpHandler(cache, filter, balancer, _stats);
            _httpsHandler = new HttpsTunnelHandler(_stats);
            _socksHandler = new Socks5Handler(_stats);
        }

        public async Task StartAsync()
        {
            TcpListener listener = new TcpListener(IPAddress.Any, _port);
            listener.Start();

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                _ = HandleClientAsync(client);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            await _connectionManager.AcquireAsync();
            try
            {
                NetworkStream stream = client.GetStream();
                string request = await RequestReader.ReadAsync(stream);

                if (request.StartsWith("CONNECT"))
                    await _httpsHandler.HandleAsync(client, request);
                else if (IsHttp(request))
                    await _httpHandler.HandleAsync(client, request);
                else
                    await _socksHandler.HandleAsync(client);
            }
            finally
            {
                _connectionManager.Release();
            }
        }

        private bool IsHttp(string request)
        {
            return request.StartsWith("GET") ||
                   request.StartsWith("POST");
        }
    }

}
