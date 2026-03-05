using System;
using System.Collections.Generic;
using System.Text;
using ProxyServer.Protocol;

namespace ProxyServer.Filtering
{
    public interface IFilter
    {
        string? Reason { get; }
        bool IsAllowed(HttpRequest request);
        bool IsAllowed(HttpResponse response);
    }
}
