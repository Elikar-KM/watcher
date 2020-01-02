namespace watcher.Infrastructure
{
    using ConcurrentCollections;

    public class DirectoryCache : IDirectoryThreadSafeCache
    {
        private readonly ConcurrentHashSet<string> cache = new ConcurrentHashSet<string>();
        
        public bool IsDirectoryInCache(string directory)
        {
            return cache.Contains(directory);
        }

        public void AddDirectoryToCache(string directory)
        {
            this.cache.Add(directory);
        }

        public int GetCacheSize()
        {
            return this.cache.Count;
        }
    }
}