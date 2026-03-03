using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.Filtering
{
    public class DomainFilter : IFilter
    {
        private readonly HashSet<string> _blockedDomains =
            new() { 
                "youtube.com",
                "discord.com",
                "telegram.com",
                "pornhub.com",
                "instagram.com",
                "cloudflare.net"
            };

        public bool IsAllowed(string request)
        {
            return !_blockedDomains.Any(d => request.Contains(d));
        }
    }
}
