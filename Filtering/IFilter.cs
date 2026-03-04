using System;
using System.Collections.Generic;
using System.Text;
using ProxyServer.Protocol;

namespace ProxyServer.Filtering
{
    public interface IFilter
    {
        bool IsAllowed(string request);
    }
}
