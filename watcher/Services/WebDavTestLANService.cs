namespace watcher.Services
{
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using watcher.Infrastructure;
    using watcher.Options;

    public class WebDavTestLANService : WebDavService, IWebDavTestLANService
    {
        public WebDavTestLANService(
            ILogger<WebDavService> logger,
            HttpClient httpClient, 
            IOptions<WatcherOptions> watcherOptions,
            IDirectoryThreadSafeCache directoryCache) 
            : base(logger, httpClient, watcherOptions, directoryCache)
        {
        }
        
        public async Task<bool> IsLANWebDavHostAvailable()
        {
            try
            {
                // Just check if the root is available.
                await this.CheckDirectoryExists("");
            }
            catch (TaskCanceledException exception)
            {
                // For some reason HttpClient throws TaskCanceledException instead of HttpRequestException...
                // https://github.com/dotnet/corefx/issues/20296
                return false;
            }

            return true;
        }
    }
}