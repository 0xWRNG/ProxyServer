using ProxyServer.Protocol;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;

namespace ProxyServer.Filtering
{
    public class FilterPipeline : IFilter
    {
        private readonly List<IFilter> _filters = new();
        public string? Reason { get; private set; }

        public void AddFilter(IFilter filter) => _filters.Add(filter);

        public bool IsAllowed(HttpRequest request)
        {
            foreach (var filter in _filters)
            {
                if (!filter.IsAllowed(request))
                {
                    Reason = filter.Reason;
                    return false;
                }
            }
            return true;
        }
        public bool IsAllowed(HttpResponse response)
        {
            foreach (var filter in _filters)
            {
                if (!filter.IsAllowed(response))
                {
                    Reason = filter.Reason;
                    return false;
                }
            }
            return true;
        }
        public bool HasAnyFilter() => _filters.Any();
    }
}
