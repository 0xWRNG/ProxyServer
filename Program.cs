using System.Threading.Tasks;
using ProxyServer.Core;
class Program
{
    static async Task Main()
    {
        var proxy = new ProxyServer.Core.ProxyServer(8888);
        await proxy.StartAsync();
    }
}