using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ProxyServer.Utils
{
    public static class ResponseHelper
    {
        public static async Task SendAsync(NetworkStream stream, byte[] data)
        {
            await stream.WriteAsync(data, 0, data.Length);
        }
        public static async Task SendStatusAsync(
            NetworkStream stream,
            int statusCode,
            string reasonPhrase,
            string body = "")
        {
            string response =
                $"HTTP/1.1 {statusCode} {reasonPhrase}\r\n" +
                $"Content-Length: {Encoding.UTF8.GetByteCount(body)}\r\n" +
                "Connection: close\r\n" +
                "\r\n" +
                body;

            byte[] bytes = Encoding.UTF8.GetBytes(response);
            await SendAsync(stream, bytes);
        }
        public static Task SendForbidden(NetworkStream stream)
            => SendStatusAsync(stream, 403, "Forbidden");

        public static Task SendBadRequest(NetworkStream stream)
            => SendStatusAsync(stream, 400, "Bad Request");

        public static Task SendBadGateway(NetworkStream stream)
            => SendStatusAsync(stream, 502, "Bad Gateway");

        public static async Task SendConnectionEstablished(NetworkStream stream)
        {
            string response =
            "HTTP/1.1 200 Connection Established\r\n" +
            "Proxy-Agent: MyProxy/1.0\r\n" +
            "\r\n";

            byte[] bytes = Encoding.ASCII.GetBytes(response);
            await SendAsync(stream, bytes);
        }
    }
}
