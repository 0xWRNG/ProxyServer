using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ProxyServer.Utils
{
    public static class RequestReader
    {
        public static async Task<string> ReadAsync(NetworkStream stream)
        {
            var buffer = new byte[8192];
            using var ms = new MemoryStream();

            while (true)
            {
                int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                if (bytesRead == 0)
                    break;

                ms.Write(buffer, 0, bytesRead);
                if (HeadersComplete(ms))
                    break;

                if (ms.Length > 64 * 1024)
                    throw new InvalidOperationException("[ERR]: Header too large");
            }

            return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
        }

        private static bool HeadersComplete(MemoryStream ms)
        {
            if (ms.Length < 4)
                return false;

            var span = new ReadOnlySpan<byte>(ms.GetBuffer(), 0, (int)ms.Length);

            for (int i = 3; i < span.Length; i++)
            {
                if (span[i - 3] == '\r' &&
                    span[i - 2] == '\n' &&
                    span[i - 1] == '\r' &&
                    span[i] == '\n')
                {
                    return true;
                }
            }

            return false;
        }
        private static string? ParseHost(string request, out int port)
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
    }
}
