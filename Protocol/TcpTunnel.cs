using ProxyServer.Monitoring;
using System.IO;
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

        public async Task StartAsync(Stream clientStream, Stream remoteStream, string host, int port)
        {
            _logger.Log(LogLevels.Transport, $"Starting transparent tunnel for {host}:{port}");

            var clientToRemote = PumpAsync(clientStream, remoteStream, "Client -> Remote");
            var remoteToClient = PumpAsync(remoteStream, clientStream, "Remote -> Client");

            try
            {
                await Task.WhenAny(clientToRemote, remoteToClient);
            }
            finally
            {
                clientStream.Close();
                remoteStream.Close();
                _logger.Log(LogLevels.Transport, $"Tunnel closed for {host}:{port}");
            }
        }

        private async Task PumpAsync(Stream input, Stream output, string direction)
        {
            var buffer = new byte[8192];
            try
            {
                int bytesRead;
                while ((bytesRead = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    await output.WriteAsync(buffer, 0, bytesRead);
                }
            }
            catch (IOException) {}
            catch (Exception ex)
            {
                _logger.Log(LogLevels.Transport, $"[{direction}] Tunnel pump failed: {ex.Message}");
                throw;
            }
            finally
            {
                _logger.Log(LogLevels.Transport, $"[{direction}] Pumping finished");
            }
        }
    }
}
