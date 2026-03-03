using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.LoadBalancing
{
    public interface ILoadBalancer
    {
        string GetNextServer();
    }
}
