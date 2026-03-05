using System;
using System.Collections.Generic;
using System.Text;

namespace ProxyServer.Cache
{
    public class CacheItem
    {
        public byte[] Data { get; set; }
        public DateTime? Expiry { get; set; }
        public string? Etag { get; set; }
        public DateTime? LastModified { get; set; }

    }
    public class LruCache : ICache
    {
        private readonly int _capacity;
        private readonly Dictionary<string, CacheItem> _cache;
        private readonly LinkedList<string> _lruList = new();


        
        public LruCache(int capacity = 100)
        {
            _capacity = capacity;
            _cache = new Dictionary<string, CacheItem>();
        }

        public bool Get(string key, out CacheItem value)
        {
            value = null;
            if (_cache.TryGetValue(key, out var item))
            {
                if (item.Expiry.HasValue && item.Expiry.Value < DateTime.UtcNow)
                {
                    Remove(key);
                    return false;
                }
                _lruList.Remove(key);
                _lruList.AddFirst(key);

                value = item;
                return true;
            }
            return false;
        }
        public void Save(string key, byte[] data, string eTag = null, DateTime? lastModified = null, int? maxAgeSeconds = null)
        {
            if (_cache.Count >= _capacity)
            {
                EvictLRU();
            }
            var item = new CacheItem
            {
                Data = data,
                Expiry = maxAgeSeconds.HasValue ? DateTime.UtcNow.AddSeconds(maxAgeSeconds.Value) : null,
                Etag = eTag,
                LastModified = lastModified
            };

            _cache[key] = item;
            _lruList.Remove(key);
            _lruList.AddFirst(key);
        }
        private void EvictLRU()
        {
            if (_lruList.Count == 0) return;
            string lruKey = _lruList.Last.Value;
            _lruList.RemoveLast();
            _cache.Remove(lruKey);
        }
        public void Remove(string key)
        {
            _cache.Remove(key);
            _lruList.Remove(key);
        }

        public void Clear()
        {
            _cache.Clear();
            _lruList.Clear();
        }
    }
}
