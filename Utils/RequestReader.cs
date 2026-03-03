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
            int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);

            return Encoding.UTF8.GetString(buffer, 0, bytesRead);
        }
    }
}
