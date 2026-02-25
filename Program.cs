using System.Threading.Tasks;

class Program
{
    static async Task Main()
    {
        var proxy = new ProxyServer(8888, true);
        await proxy.StartAsync();
    }
}