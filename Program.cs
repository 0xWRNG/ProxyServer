using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ProxyServer.Core;
using ProxyServer.Filtering;

class Program
{
    static async Task Main(string[] args)
    {
        var (port, useCache, useFilter, reverseProxy, backends, blockedDomains, blockedUrls, blockedMimeTypes) = ParseArguments(args);

        CancellationTokenSource? cts = null;
        ProxyServer.Core.ProxyServer? proxy = null;
        Task? proxyTask = null;

        Console.CancelKeyPress += (sender, eventArgs) =>
        {
            Console.WriteLine("Shutting down proxy...");
            cts?.Cancel();
            eventArgs.Cancel = true;
            System.Environment.Exit(0);

        };

        await StartProxyAsync();

        Console.WriteLine("Press Ctrl+R to restart proxy, Ctrl+L to clear logs, Ctrl+C to exit.");

        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                if (key.Key == ConsoleKey.R) // Ctrl+R for restart
                {
                    Console.WriteLine("\nRestarting proxy...");
                    cts?.Cancel(); // Signal current proxy to stop
                    if (proxyTask != null) await proxyTask; // Wait for it to stop
                    await StartProxyAsync();
                    Console.WriteLine("Proxy restarted. Press Ctrl+R to restart, Ctrl+L to clear logs, Ctrl+C to exit.");
                }
                else if (key.Key == ConsoleKey.L) // Ctrl+L for clear logs
                {
                    Console.Clear();
                    Console.WriteLine("Logs cleared. Press Ctrl+R to restart, Ctrl+L to clear logs, Ctrl+C to exit.");
                }
            }
            else if (key.Key == ConsoleKey.C && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                break; // Exit loop on Ctrl+C
            }
        }

        // Ensure proxy is stopped on exit
        cts?.Cancel();
        if (proxyTask != null) await proxyTask;

        Console.WriteLine("Proxy application exited.");

        async Task StartProxyAsync()
        {
            cts = new CancellationTokenSource();

            FilterPipeline? filterPipeline = null;
            if (useFilter)
            {
                filterPipeline = new FilterPipeline();
                if (blockedDomains != null && blockedDomains.Any())
                {
                    filterPipeline.AddFilter(new DomainFilter(blockedDomains));
                }
                if (blockedUrls != null && blockedUrls.Any())
                {
                    filterPipeline.AddFilter(new UrlFilter(blockedUrls));
                }
                if (blockedMimeTypes != null && blockedMimeTypes.Any())
                {
                    filterPipeline.AddFilter(new MimeFilter(blockedMimeTypes));
                }
            }

            proxy = new ProxyServer.Core.ProxyServer(
                port: port,
                useCache: useCache,
                useFilter: useFilter && filterPipeline != null && filterPipeline.HasAnyFilter(),
                reverseProxy: reverseProxy,
                backends: backends,
                filterPipeline: filterPipeline,
                cancellationToken: cts.Token // Pass cancellation token
            );
            proxyTask = proxy.StartAsync();
        }
    }

    static (int port, bool useCache, bool useFilter, bool reverseProxy, List<string>? backends, List<string>? blockedDomains, List<string>? blockedUrls, List<string>? blockedMimeTypes) ParseArguments(string[] args)
    {
        int port = 8888;
        bool useCache = true;
        bool useFilter = true;
        bool reverseProxy = false;
        List<string>? backends = null;
        List<string>? blockedDomains = null;
        List<string>? blockedUrls = null;
        List<string>? blockedMimeTypes = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLower())
            {
                case "--port":
                    if (i + 1 < args.Length && int.TryParse(args[i + 1], out int p))
                    {
                        port = p;
                        i++;
                    }
                    break;
                case "--use-cache":
                    useCache = true;
                    break;
                case "--no-cache":
                    useCache = false;
                    break;
                case "--reverse":
                    reverseProxy = true;
                    break;
                case "--backends":
                    if (i + 1 < args.Length)
                    {
                        backends = args[i + 1].Split(",").ToList();
                        i++;
                    }
                    break;
                case "--block-domains":
                    if (i + 1 < args.Length)
                    {
                        blockedDomains = args[i + 1].Split(",").ToList();
                        i++;
                    }
                    break;
                case "--block-urls":
                    if (i + 1 < args.Length)
                    {
                        blockedUrls = args[i + 1].Split(",").ToList();
                        i++;
                    }
                    break;
                case "--block-mimes":
                    if (i + 1 < args.Length)
                    {
                        blockedMimeTypes = args[i + 1].Split(",").ToList();
                        i++;
                    }
                    break;
                case "--no-filter":
                    useFilter = false;
                    break;
            }
        }
        return (port, useCache, useFilter, reverseProxy, backends, blockedDomains, blockedUrls, blockedMimeTypes);
    }
}
