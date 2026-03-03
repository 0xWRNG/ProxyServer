using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.Monitoring
{
    public class StatisticsCollector
    {
        private long _totalRequests;
        public long TotalRequests => _totalRequests;
        public void IncrementRequests()
        {
            Interlocked.Increment(ref _totalRequests);
        }
    }
}
