using System;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Text;
public class ProxyServer
{
    private readonly int _port;
    private TcpListener? _listener;
    private CacheManager? _cacheManager;
    private bool _useCache = false;
    public ProxyServer(int port, bool cache)
    {
        _port = port;
        _useCache = cache;
        if (_useCache)
        {
            _cacheManager = new CacheManager();
        }
    }
    public async Task StartAsync()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        Console.WriteLine($"[i]: Proxy started on port {_port}");
        while (true)
        {
            TcpClient client = await _listener.AcceptTcpClientAsync();
            _ = Task.Run(() => HandleClientAsync(client));
        }
    }

    private async Task HandleClientAsync(TcpClient client)
    {
        using (client)
        using (NetworkStream clientStream = client.GetStream())
        {
            try
            {
                string requestText = await ReadHttpRequest(clientStream);
                if (string.IsNullOrEmpty(requestText))
                    return;

                string firstLine = requestText.Split("\r\n")[0];
                string method = firstLine.Split(' ')[0].ToUpper();

                switch (method)
                {
                    case "CONNECT":
                        await HandleHTTPS(clientStream, requestText);
                        break;
                    default:
                        await HandleHTTP(clientStream, requestText);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[!]: {ex.Message}");
            }
        }
    }

    #region Handle methods
    //private async Task HandleHTTP(NetworkStream clientStream, string requestText)
    //{
    //    string? host = ParseHost(requestText, out int port);
    //    if (host == null)
    //    {
    //        Console.WriteLine("[?]: Host not found");
    //        return;
    //    }
    //    string firstLine = requestText.Split("\r\n")[0];
    //    string url = firstLine.Split(' ')[1];

    //    if (_useCache && _cacheManager!=null && _cacheManager.Get(url, out byte[] cachedData))
    //    {
    //        Console.WriteLine($"[i]: Using cache for: {url}");
    //        await clientStream.WriteAsync(cachedData, 0, cachedData.Length);
    //        return;
    //    }

    //    Console.WriteLine($"[i]: HTTP to {host}:{port}");

    //    string fixedRequest = FixRequestLine(requestText);
    //    fixedRequest = fixedRequest.Replace("Connection: keep-alive", "Connection: close");
    //    byte[] requestBytes = Encoding.ASCII.GetBytes(fixedRequest);

    //    using (TcpClient server = new TcpClient())
    //    {
    //        await server.ConnectAsync(host, port);
    //        using (NetworkStream serverStream = server.GetStream())
    //        {
    //            await serverStream.WriteAsync(requestBytes, 0, requestBytes.Length);
    //            using (MemoryStream responseBuffer = new MemoryStream())
    //            {
    //                byte[] buffer = new byte[8192];
    //                while (true)
    //                {
    //                    int bytesRead = await serverStream.ReadAsync(buffer, 0, buffer.Length);
    //                    if (bytesRead == 0) break;

    //                    responseBuffer.Write(buffer, 0, bytesRead);
    //                    await clientStream.WriteAsync(buffer, 0, bytesRead);
    //                }
    //                if (_useCache && _cacheManager != null)
    //                {
    //                    _cacheManager.Save(url, responseBuffer.ToArray());
    //                }
    //            }
    //        }
    //    }
    //}

    private async Task HandleHTTP(NetworkStream clientStream, string requestText)
    {
        string? host = ParseHost(requestText, out int port);
        if (host == null)
        {
            Console.WriteLine("[!]: Host not found in request");
            return;
        }
        Console.WriteLine($"[i]: HTTP request to {host}:{port}");

        string[] lines = requestText.Split(new[] { "\r\n" }, StringSplitOptions.None);
        string[] parts = lines[0].Split(' ');

        string path = parts[1];
        if (parts[1].StartsWith("http://") || parts[1].StartsWith("https://"))
        {
            Uri uri = new Uri(parts[1]);
            path = uri.PathAndQuery;
        }

        lines[0] = $"{parts[0]} {path} {parts[2]}";

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("Connection:", StringComparison.OrdinalIgnoreCase) ||
                lines[i].StartsWith("Proxy-Connection:", StringComparison.OrdinalIgnoreCase))
            {
                lines[i] = "Connection: close";
            }
        }

        string fixedRequest = string.Join("\r\n", lines) + "\r\n\r\n";
        byte[] requestBytes = Encoding.ASCII.GetBytes(fixedRequest);

        if (_useCache && _cacheManager != null && _cacheManager.Get(parts[1], out byte[] cachedData))
        {
            Console.WriteLine($"[i]: Using cache for {parts[1]} ({cachedData.Length} bytes)");
            await clientStream.WriteAsync(cachedData, 0, cachedData.Length);
            return;
        }

        using (TcpClient server = new TcpClient())
        {
            await server.ConnectAsync(host, port);
            using (NetworkStream serverStream = server.GetStream())
            using (MemoryStream responseBuffer = _useCache ? new MemoryStream() : null!)
            {
                await serverStream.WriteAsync(requestBytes, 0, requestBytes.Length);
                await serverStream.FlushAsync();

                byte[] buffer = new byte[8192];
                int bytesRead;
                while ((bytesRead = await serverStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await clientStream.WriteAsync(buffer, 0, bytesRead);

                    if (_useCache && _cacheManager != null)
                    {
                        responseBuffer.Write(buffer, 0, bytesRead);
                    }
                }

                if (_useCache && _cacheManager != null)
                {
                    _cacheManager.Save(parts[1], responseBuffer.ToArray());
                    Console.WriteLine($"[i]: Saving cache ({responseBuffer.Length} bytes)");
                }
            }
        }
    }
    private async Task HandleHTTPS(NetworkStream clientStream, string requestText)
    {
        string firstLine = requestText.Split("\r\n")[0];
        string hostPort = firstLine.Split(' ')[1];
        string[] hp = hostPort.Split(':');
        string host = hp[0];
        int port = int.Parse(hp[1]);

        Console.WriteLine($"[i] HTTPS CONNECT to {host}:{port}");

        using (TcpClient server = new TcpClient())
        {
            await server.ConnectAsync(host, port);
            using (NetworkStream serverStream = server.GetStream())
            {
                byte[] ok = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                await clientStream.WriteAsync(ok, 0, ok.Length);

                await RelayStreams(clientStream, serverStream);
            }
        }
    }
    #endregion
    private async Task RelayStreams(NetworkStream stream1, NetworkStream stream2)
    {
        var t1 = Task.Run(async () =>
        {
            byte[] buffer = new byte[8192];
            while (true)
            {
                int bytesRead = await stream1.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;
                await stream2.WriteAsync(buffer, 0, bytesRead);
            }
        });

        var t2 = Task.Run(async () =>
        {
            byte[] buffer = new byte[8192];
            while (true)
            {
                int bytesRead = await stream2.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0) break;
                await stream1.WriteAsync(buffer, 0, bytesRead);
            }
        });

        await Task.WhenAny(t1, t2);
    }
    private string? ParseHost(string request, out int port)
    {
        port = 80;
        using (StringReader reader = new StringReader(request))
        {
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                {
                    string host = line.Substring(5).Trim();
                    if (host.Contains(":"))
                    {
                        var parts = host.Split(':');
                        port = int.Parse(parts[1]);
                        return parts[0];
                    }
                    return host;
                }

            }
        }
        return null;
    }

    private string FixRequestLine(string request)
    {
        var lines = request.Split("\r\n");
        if (lines.Length == 0)
            return request;

        var firstLineParts = lines[0].Split(' ');
        if (firstLineParts.Length < 3)
            return request;

        string method = firstLineParts[0];
        string url = firstLineParts[1];
        string version = firstLineParts[2];

        if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
        {
            lines[0] = $"{method} {uri.PathAndQuery} {version}";
        }

        return string.Join("\r\n", lines);
    }

    private async Task<string> ReadHttpRequest(NetworkStream stream)
    {
        byte[] buffer = new byte[8192];
        MemoryStream ms = new MemoryStream();
        while (true)
        {
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
            if (bytesRead == 0) break;
            ms.Write(buffer, 0, bytesRead);
            string temp = Encoding.ASCII.GetString(ms.ToArray());
            if (temp.Contains("\r\n\r\n")) break;
        }
        return Encoding.ASCII.GetString(ms.ToArray());
    }
}
