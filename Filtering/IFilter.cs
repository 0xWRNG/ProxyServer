using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.Filtering
{
    public interface IFilter
    {
        bool IsAllowed(string request);
    }
}
