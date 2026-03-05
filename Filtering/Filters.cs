using System;
using System.Collections.Generic;
using System.Text;
using ProxyServer.Protocol;

namespace ProxyServer.Filtering
{

    public class DomainFilter : IFilter
    {
        public string? Reason { get; private set; }
        private readonly HashSet<string> _blockedDomains =
            new() {
                "youtube.com",
                "discord.com",
                "telegram.com",
                "pornhub.com",
                "instagram.com",
            };
        public DomainFilter(IEnumerable<string> domains)
        {
            _blockedDomains = domains.ToHashSet();
        }
        public bool IsAllowed(HttpRequest request)
        {
            if (_blockedDomains.Contains(request.Host))
            {
                Reason = $"Domain blocked: {request.Host}";
                return false;
            }
            return true;
        }
        public bool IsAllowed(HttpResponse response) => true;
    }
    public class UrlFilter : IFilter
    {
        private readonly HashSet<string> _blockedPaths = new()
        {
            "/ads/",
            "/tracking/",
            "/analytics/"
        };
        public string? Reason { get; private set; }

        public UrlFilter(IEnumerable<string> paths)
        {
            _blockedPaths = paths.ToHashSet();
        }

        public bool IsAllowed(HttpRequest request)
        {
            foreach (var p in _blockedPaths)
            {
                if (request.Path.Contains(p, StringComparison.OrdinalIgnoreCase))
                {
                    Reason = $"Path blocked: {request.Path}";
                    return false;
                }
            }
            return true;
        }
        public bool IsAllowed(HttpResponse response) => true;
    }
    public class MimeFilter : IFilter
    {
        private readonly HashSet<string> _blockedMimeTypes;
        public string? Reason { get; private set; }
        public MimeFilter(IEnumerable<string> blockedMimeTypes)
        {
            _blockedMimeTypes = new HashSet<string>(blockedMimeTypes, StringComparer.OrdinalIgnoreCase);
        }
        public bool IsAllowed(HttpRequest request) => true;
        public bool IsAllowed(HttpResponse response)
        {
            if (response.Headers.TryGetValue("Content-Type", out var type))
            {
                if (_blockedMimeTypes.Contains(type))
                {
                    Reason = $"MIME blocked: {type}";
                    return false;
                }
            }
            return true;
        }
    }
}
