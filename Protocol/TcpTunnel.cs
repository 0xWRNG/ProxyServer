using ProxyServer.Monitoring;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ProxyServer.Protocol
{
    public class TcpTunnel
    {
        private readonly Logger _logger;
        public TcpTunnel(Logger logger)
        {
            _logger = logger;
        }
        public async Task StartAsync(
            NetworkStream clientStream,
            NetworkStream serverStream, string host, int port)
        {
            
            var clientToServer = PumpAsync(clientStream, serverStream);
            var serverToClient = PumpAsync(serverStream, clientStream);
            
            await Task.WhenAny(clientToServer, serverToClient);
            _logger.Log(LogLevels.Transport, "Tunnel closed {0}:{1}",host, port);
        }

        private async Task PumpAsync(
            NetworkStream input,
            NetworkStream output)
        {
            var buffer = new byte[8192];

            try
            {
                while (true)
                {
                    int bytesRead = await input.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0)
                        break;

                    await output.WriteAsync(buffer, 0, bytesRead);
                }
            }
            catch(Exception ex)
            {
                _logger.Log(LogLevels.Transport,$"Tunnel error: {0}", ex.Message);
            }
        }
    }
}
