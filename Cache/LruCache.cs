using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.Cache
{
    public class LruCache : ICache
    {
        private readonly int _capacity;
        private readonly Dictionary<string, byte[]> _cache;

        public LruCache(int capacity)
        {
            _capacity = capacity;
            _cache = new Dictionary<string, byte[]>();
        }

        public bool Get(string key, out byte[] value)
            => _cache.TryGetValue(key, out value);

        public void Save(string key, byte[] value)
        {
            if (_cache.Count >= _capacity)
            {
                var firstKey = _cache.Keys.First();
                _cache.Remove(firstKey);
            }

            _cache[key] = value;
        }
    }
}
