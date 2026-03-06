using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ProxyServer.Utils
{
    public static class RequestReader
    {
        public static async Task<string> ReadAsync(Stream stream)
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
    }
}
