using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.LoadBalancing
{
    public class Server
    {
        public string Host { get; set; }
        public int Port { get; set; }
        public Server(string host, int port)
        {
            this.Host = host;
            this.Port = port;
        }
    }
    public interface ILoadBalancer
    {
        Server GetNextServer();
    }
}
