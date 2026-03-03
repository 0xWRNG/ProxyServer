using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.Cache
{
    public interface ICache
    {
        bool Get(string key, out byte[] value);
        void Save(string key, byte[] value);
    }
}
