using ProxyServer.Monitoring;
using ProxyServer.Utils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;


namespace ProxyServer.Protocol
{
    public static class StreamHelper
    {
        public static async Task<int> ReadByteAsync(this Stream stream)
        {
            byte[] buffer = new byte[1];
            int read = await stream.ReadAsync(buffer, 0, 1);
            if (read == 0) return -1;
            return buffer[0];
        }
    }
    public class Socks5Handler : IProtocolHandler
    {
        private readonly TcpTunnel _tunnel;
        private readonly Logger _logger;
        private readonly StatisticsCollector _stats;

        public Socks5Handler(Logger logger, StatisticsCollector stats)
        {
            _stats = stats;
            _logger = logger;
            _tunnel = new TcpTunnel(logger);
        }
        public async Task HandleAsync(TcpClient client, string? firstByteStr = null)
        {
            var originalStream = client.GetStream();

            if (!byte.TryParse(firstByteStr, out byte fb))
            {
                _logger.Log(LogLevels.Transport, "Invalid first byte string");
                return;
            }
            PushbackStream stream = new PushbackStream(originalStream, fb);

            try
            {
                await DoHandshakeAsync(stream);
                var (cmd, targetHost, targetPort) = await ReadCommandAsync(stream);

                switch (cmd)
                {
                    case 0x01: // CONNECT
                        await HandleConnectAsync(client, stream, targetHost, targetPort, new byte[] { fb });
                        break;
                    case 0x02: // BIND
                        await HandleBindAsync(client, stream, targetHost, targetPort);
                        break;
                    case 0x03: // UDP ASSOCIATE
                        await HandleUdpAssociateAsync(client, stream);
                        break;
                    default:
                        await SendReplyAsync(stream, 0x07);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevels.Transport, $"SOCKS error: {ex.Message}");
            }
        }
        private async Task<(byte, string targetHost, int targetPort)> ReadCommandAsync(Stream stream)
        {
            string targetHost = "";
            int targetPort = 0;

            int ver = await StreamHelper.ReadByteAsync(stream);
            int cmd = await StreamHelper.ReadByteAsync(stream);
            await StreamHelper.ReadByteAsync(stream);
            int atyp = await StreamHelper.ReadByteAsync(stream);

            switch (atyp)
            {
                case 0x01: // IPv4
                    byte[] ipBytes = new byte[4];
                    await stream.ReadExactlyAsync(ipBytes, 0, 4);
                    targetHost = new IPAddress(ipBytes).ToString();
                    break;
                case 0x03: // Domain
                    int len = await StreamHelper.ReadByteAsync(stream);
                    byte[] domain = new byte[len];
                    await stream.ReadExactlyAsync(domain, 0, len);
                    targetHost = System.Text.Encoding.ASCII.GetString(domain);
                    break;
                case 0x04: // IPv6
                    byte[] ipv6 = new byte[16];
                    await stream.ReadExactlyAsync(ipv6, 0, 16);
                    targetHost = new IPAddress(ipv6).ToString();
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported address type {atyp:X2}");
            }

            byte[] portBytes = new byte[2];
            await stream.ReadExactlyAsync(portBytes, 0, 2);
            targetPort = (portBytes[0] << 8) | portBytes[1];

            _logger.Log(LogLevels.Transport, $"SOCKS {GetVerboseCommand(cmd)} {targetHost}:{targetPort}, ATYP={atyp:X2}");
            return ((byte)cmd, targetHost, targetPort);
        }

        private async Task DoHandshakeAsync(Stream stream)
        {
            int ver = await StreamHelper.ReadByteAsync(stream);
            if (ver != 0x05)
                throw new InvalidOperationException($"Invalid SOCKS version {ver:X2}");

            int nMethods = await StreamHelper.ReadByteAsync(stream);
            if (nMethods <= 0) throw new InvalidOperationException("No methods in handshake");

            byte[] methods = new byte[nMethods];
            await stream.ReadExactlyAsync(methods, 0, nMethods);

            if (!methods.Contains((byte)0x00))
            {
                await stream.WriteAsync(new byte[] { 0x05, 0xFF });
                throw new InvalidOperationException("No supported auth methods");
            }

            await stream.WriteAsync(new byte[] { 0x05, 0x00 });
            await stream.FlushAsync();
            _logger.Log(LogLevels.Transport, "SOCKS handshake completed");
        }
        private async Task HandleConnectAsync(TcpClient client, Stream stream, string host, int port, byte[] initalData)
        {
            TcpClient? remote = null;
            try
            {
                remote = new TcpClient();
                await remote.ConnectAsync(host, port);

                var localEndPoint = (IPEndPoint)remote.Client.LocalEndPoint!;

                await SendReplyAsync(stream, 0x00, localEndPoint.Address.ToString(), localEndPoint.Port);
                await stream.FlushAsync();

                var tunnel = new TcpTunnel(_logger);
                await tunnel.StartAsync(stream, remote.GetStream(), host, port);
            }
            catch (Exception ex)
            {
                await SendReplyAsync(stream, 0x05); // refuse
                _logger.Log(LogLevels.Transport, $"SOCKS: Connection refused: {ex.Message}");
            }
            finally
            {
                remote?.Close();
                client.Close();
            }
        }


        private async Task HandleBindAsync(TcpClient client, Stream stream, string expectedHost, int expectedPort)
        {
            TcpListener? listener = null;
            TcpClient? incomingClient = null;
            try
            {
                listener = new TcpListener(IPAddress.Any, 0);
                listener.Start();

                var listeningEndPoint = (IPEndPoint)listener.LocalEndpoint!;
                _logger.Log(LogLevels.Protocol, $"[BIND]: Started listening on {listeningEndPoint.Address}:{listeningEndPoint.Port}");

                await SendReplyAsync(stream, 0x00, listeningEndPoint.Address.ToString(), listeningEndPoint.Port);
                _logger.Log(LogLevels.Transport, "Sent 1st BIND reply (Success, Listener ready)");

                var acceptTask = listener.AcceptTcpClientAsync();
                if (await Task.WhenAny(acceptTask, Task.Delay(TimeSpan.FromSeconds(30))) != acceptTask)
                {
                    _logger.Log(LogLevels.Protocol, "[BIND]: Timed out waiting for an incoming connection.");
                    await SendReplyAsync(stream, 0x01); // General failure
                    return;
                }
                incomingClient = await acceptTask;
                var incomingEndPoint = (IPEndPoint)incomingClient.Client.RemoteEndPoint!;
                _logger.Log(LogLevels.Protocol, $"[BIND]: Accepted connection from {incomingEndPoint.Address}:{incomingEndPoint.Port}");

                await SendReplyAsync(stream, 0x00, incomingEndPoint.Address.ToString(), incomingEndPoint.Port);
                _logger.Log(LogLevels.Transport, "Sent 2nd BIND reply (Success, Connection established)");

                var tunnel = new TcpTunnel(_logger);
                await tunnel.StartAsync(stream, incomingClient.GetStream(), incomingEndPoint.Address.ToString(), incomingEndPoint.Port);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevels.Protocol, $"[BIND]: Failed with error: {ex.Message}");
                await SendReplyAsync(stream, 0x01); // General failure
            }
            finally
            {
                listener?.Stop();
                incomingClient?.Close();
            }
        }



        private async Task HandleUdpAssociateAsync(TcpClient client, Stream stream)
        {
            UdpClient? udpRelay = null;
            CancellationTokenSource? cts = null;
            try
            {
                udpRelay = new UdpClient(0);
                var relayEndPoint = (IPEndPoint)udpRelay.Client.LocalEndPoint!;
                _logger.Log(LogLevels.Protocol, $"[UDP ASSOCIATE]: Relay created at {relayEndPoint.Address}:{relayEndPoint.Port}");

                await SendReplyAsync(stream, 0x00, relayEndPoint.Address.ToString(), relayEndPoint.Port);
                _logger.Log(LogLevels.Transport, "Sent UDP ASSOCIATE reply (Success, Relay ready)");

                cts = new CancellationTokenSource();
                var pumpTask = UdpPumpAsync(udpRelay, cts.Token);

                _logger.Log(LogLevels.Protocol, "[UDP ASSOCIATE]: Monitoring control connection...");
                byte[] buffer = new byte[1];
                await stream.ReadExactlyAsync(buffer, 0, 1, cts.Token);

            }
            catch (OperationCanceledException)
            {
                _logger.Log(LogLevels.Transport, "[UDP ASSOCIATE]: Task was cancelled.");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevels.Protocol, $"[UDP ASSOCIATE]: Failed with error: {ex.Message}");
                if (stream.CanWrite)
                {
                    await SendReplyAsync(stream, 0x01);
                }
            }
            finally
            {
                _logger.Log(LogLevels.Protocol, "[UDP ASSOCIATE]: Shutting down relay...");

                if (cts != null && !cts.IsCancellationRequested)
                {
                    cts.Cancel();
                }

                udpRelay?.Close();

                _logger.Log(LogLevels.Protocol, "[UDP ASSOCIATE]: Relay closed.");
            }
        }

        private async Task UdpPumpAsync(UdpClient udpRelay, CancellationToken token)
        {
            _logger.Log(LogLevels.Transport, $"[UDP PUMP]: Started on {udpRelay.Client.LocalEndPoint}");

            IPEndPoint? clientUdpEndPoint = null;

            var activeTunnels = new Dictionary<IPEndPoint, IPEndPoint>();

            while (!token.IsCancellationRequested)
            {
                try
                {
                    UdpReceiveResult result = await udpRelay.ReceiveAsync(token);
                    var sourceEndPoint = result.RemoteEndPoint;

                    if (clientUdpEndPoint == null)
                    {
                        clientUdpEndPoint = sourceEndPoint;
                        _logger.Log(LogLevels.Transport, $"[UDP PUMP]: Detected client UDP endpoint: {clientUdpEndPoint}");
                    }

                    if (sourceEndPoint.Equals(clientUdpEndPoint))
                    {
                        var (targetHost, targetPort, payload) = ParseSocksUdpPacket(result.Buffer);
                        if (targetHost != null && payload.Length > 0)
                        {
                            var targetIpAddress = (await Dns.GetHostAddressesAsync(targetHost)).FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork);
                            if (targetIpAddress == null) continue;

                            var targetEndPoint = new IPEndPoint(targetIpAddress, targetPort);

                            activeTunnels[targetEndPoint] = clientUdpEndPoint;

                            _logger.Log(LogLevels.Transport, $"[UDP PUMP]: Relaying {payload.Length} bytes from client to {targetEndPoint}");
                            await udpRelay.SendAsync(payload, payload.Length, targetEndPoint);
                        }
                    }
                    else if (activeTunnels.TryGetValue(sourceEndPoint, out var destinationClientEndPoint))
                    {
                        _logger.Log(LogLevels.Transport, $"[UDP PUMP]: Relaying {result.Buffer.Length} bytes from {sourceEndPoint} to client");
                        byte[] responsePacket = CreateSocksUdpPacket(sourceEndPoint.Address.ToString(), (ushort)sourceEndPoint.Port, result.Buffer);
                        await udpRelay.SendAsync(responsePacket, responsePacket.Length, destinationClientEndPoint);
                    }
                    else
                    {
                        _logger.Log(LogLevels.Transport, $"[UDP PUMP]: Received unexpected packet from {sourceEndPoint}. Ignored.");
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevels.Transport, $"[UDP PUMP]: Error: {ex.Message}");
                }
            }
            _logger.Log(LogLevels.Transport, "[UDP PUMP]: Stopped.");
        }



        private (string? host, int port, byte[] payload) ParseSocksUdpPacket(byte[] packet)
        {
            using (var stream = new MemoryStream(packet))
            using (var reader = new BinaryReader(stream))
            {
                try
                {
                    if (packet.Length < 10) // 2(RSV)+1(FRAG)+1(ATYP)+4(IPv4)+2(PORT) = 10
                    {
                        _logger.Log(LogLevels.Transport, "[UDP PARSE]: Packet is too short.");
                        return (null, 0, Array.Empty<byte>());
                    }

                    reader.ReadBytes(3);

                    byte atyp = reader.ReadByte();
                    string? host = null;
                    int payloadOffset;

                    switch (atyp)
                    {
                        case 0x01: // IPv4
                            byte[] ipv4Bytes = reader.ReadBytes(4);
                            host = new IPAddress(ipv4Bytes).ToString();
                            payloadOffset = 4 + 4 + 2; // RSV+FRAG+ATYP + IPv4 + Port
                            break;

                        case 0x03: // Domain Name
                            byte domainLength = reader.ReadByte();
                            byte[] domainBytes = reader.ReadBytes(domainLength);
                            host = Encoding.ASCII.GetString(domainBytes);
                            payloadOffset = 4 + 1 + domainLength + 2; // RSV+FRAG+ATYP + Len+Domain + Port
                            break;

                        case 0x04: // IPv6
                            byte[] ipv6Bytes = reader.ReadBytes(16);
                            host = new IPAddress(ipv6Bytes).ToString();
                            payloadOffset = 4 + 16 + 2; // RSV+FRAG+ATYP + IPv6 + Port
                            break;

                        default:
                            _logger.Log(LogLevels.Transport, $"[UDP PARSE]: Unsupported address type {atyp:X2}.");
                            return (null, 0, Array.Empty<byte>());
                    }

                    ushort port = (ushort)IPAddress.NetworkToHostOrder(reader.ReadInt16());

                    int headerLength = (int)stream.Position;
                    byte[] payload = new byte[packet.Length - headerLength];
                    Array.Copy(packet, headerLength, payload, 0, payload.Length);

                    return (host, port, payload);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevels.Transport, $"[UDP PARSE]: Error parsing packet: {ex.Message}");
                    return (null, 0, Array.Empty<byte>());
                }
            }
        }
        private byte[] CreateSocksUdpPacket(string sourceHost, ushort sourcePort, byte[] payload)
        {
            using (var stream = new MemoryStream())
            using (var writer = new BinaryWriter(stream))
            {
                // RSV (0x0000) и FRAG (0x00)
                writer.Write(new byte[] { 0x00, 0x00, 0x00 });

                // ATYP и DST.ADDR
                if (IPAddress.TryParse(sourceHost, out var ipAddress))
                {
                    if (ipAddress.AddressFamily == AddressFamily.InterNetwork) // IPv4
                    {
                        writer.Write((byte)0x01);
                        writer.Write(ipAddress.GetAddressBytes());
                    }
                    else // IPv6
                    {
                        writer.Write((byte)0x04);
                        writer.Write(ipAddress.GetAddressBytes());
                    }
                }
                else
                {
                    writer.Write((byte)0x03);
                    byte[] domainBytes = Encoding.ASCII.GetBytes(sourceHost);
                    writer.Write((byte)domainBytes.Length);
                    writer.Write(domainBytes);
                }

                writer.Write((ushort)IPAddress.HostToNetworkOrder((short)sourcePort));
                writer.Write(payload);

                return stream.ToArray();
            }
        }


        private async Task SendReplyAsync(Stream stream, byte rep, string bindAddr = "0.0.0.0", int bindPort = 0)
        {
            byte[] addrBytes;
            byte atyp;

            if (IPAddress.TryParse(bindAddr, out var ip))
            {
                addrBytes = ip.GetAddressBytes();
                atyp = (byte)(addrBytes.Length == 4 ? 0x01 : 0x04);
            }
            else
            {
                addrBytes = System.Text.Encoding.ASCII.GetBytes(bindAddr);
                atyp = 0x03;
            }
            byte[] portBytes = new byte[2] { (byte)(bindPort >> 8), (byte)(bindPort & 0xFF) };
            byte[] reply = new byte[6 + addrBytes.Length];
            reply[0] = 0x05; // VER
            reply[1] = rep;  // REP
            reply[2] = 0x00; // RSV
            reply[3] = atyp;
            Array.Copy(addrBytes, 0, reply, 4, addrBytes.Length);
            Array.Copy(portBytes, 0, reply, 4 + addrBytes.Length, 2);

            await stream.WriteAsync(reply);
            await stream.FlushAsync();
        }

        private string GetVerboseCommand(int num) 
        {
            return num switch
            {
                0x01 => "CONNECT",
                0x02 => "BIND",
                0x03 => "UDP ASSOCIATE",
                _ => "UNKNOWN"
            };
        }
    }
}