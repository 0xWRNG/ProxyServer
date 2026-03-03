using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.Core
{
    public class ConnectionManager
    {
        private readonly SemaphoreSlim _semaphore;

        public ConnectionManager(int maxConnections)
        {
            _semaphore = new SemaphoreSlim(maxConnections);
        }

        public Task AcquireAsync() => _semaphore.WaitAsync();
        public void Release() => _semaphore.Release();
    }
}
