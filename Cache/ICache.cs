using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.Cache
{
    public interface ICache
    {
        bool Get(string key, out byte[] value);
        void Save(string key, byte[] value);

        public static string GetCacheKey(string host, int port, string path) 
        {
            string key = $"{host}:{port}{path}";
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(key));
        }
    }
}
