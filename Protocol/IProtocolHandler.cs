using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ProxyServer.Protocol
{
    public interface IProtocolHandler
    {
        Task HandleAsync(TcpClient client, string? request = null);
    }
}
