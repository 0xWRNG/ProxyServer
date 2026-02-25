using System;
using System.Text;

public class CacheManager
{
    private readonly string _cacheFolder = "cache";
    private readonly string _cacheExtension = ".cache";

    public CacheManager()
    {
        if (!Directory.Exists(_cacheFolder))
        {
            Directory.CreateDirectory(_cacheFolder);
        }
    }

    private string GetCachePath(string url)
    {
        string filename = Convert.ToBase64String(Encoding.UTF8.GetBytes(url));
        return Path.Combine(_cacheFolder, filename + _cacheExtension);
    }

    public bool Get(string url, out byte[] data)
    {
        string path = GetCachePath(url);
        if (File.Exists(path))
        {
            data = File.ReadAllBytes(path);
            return true;
        }
        data = null;
        return false;
    }

    public void Save(string url, byte[] data)
    {
        string path = GetCachePath(url);
        File.WriteAllBytes(path, data);
    }
}