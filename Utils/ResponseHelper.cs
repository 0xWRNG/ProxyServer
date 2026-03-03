using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ProxyServer.Utils
{
    public static class ResponseHelper
    {
        public static async Task SendForbidden(TcpClient client)
        {
            string response =
                "HTTP/1.1 403 Forbidden\r\n" +
                "Content-Length: 0\r\n\r\n";

            await Send(client, Encoding.UTF8.GetBytes(response));
        }

        public static async Task Send(TcpClient client, byte[] data)
        {
            NetworkStream stream = client.GetStream();
            await stream.WriteAsync(data, 0, data.Length);
            client.Close();
        }
    }
}
