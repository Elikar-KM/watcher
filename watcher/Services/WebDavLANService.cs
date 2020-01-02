namespace watcher.Services
{
    using System.Net.Http;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using watcher.Infrastructure;
    using watcher.Options;

    public class WebDavLANService : WebDavService
    {
        public WebDavLANService(
            ILogger<WebDavService> logger,
            HttpClient httpClient, 
            IOptions<WatcherOptions> watcherOptions,
            IDirectoryThreadSafeCache directoryCache) 
            : base(logger, httpClient, watcherOptions, directoryCache)
        {
        }

        public override bool IsLANService()
        {
            return true;
        }
    }
}