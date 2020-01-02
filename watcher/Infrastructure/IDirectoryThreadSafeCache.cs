namespace watcher.Infrastructure
{
    public interface IDirectoryThreadSafeCache
    {
        bool IsDirectoryInCache(string directory);

        void AddDirectoryToCache(string directory);

        int GetCacheSize();
    }
}