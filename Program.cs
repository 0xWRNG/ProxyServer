using System.Threading.Tasks;
using ProxyServer.Core;
class Program
{
    static async Task Main()
    {
        var proxy = new ProxyServer.Core.ProxyServer(
            port: 8888,
            useCache: true,
            useFilter: true,
            reverseProxy: false,
            backends: null //new List<String>() {"localhost:8081", "localhost:8082" }
            );
        await proxy.StartAsync();
    }
}