namespace watcher.Services
{
    using System;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using watcher.Infrastructure;
    using watcher.Options;
    using watcher.Services.WebDAV;

    public class WebDavService : IWebDavService
    {
        private readonly HttpClient httpClient;
        private readonly WatcherOptions watcherOptions;
        private readonly IDirectoryThreadSafeCache directoryCache;
        private readonly ILogger<WebDavService> logger;

        public WebDavService(
            ILogger<WebDavService> logger,
            HttpClient httpClient, 
            IOptions<WatcherOptions> watcherOptions,
            IDirectoryThreadSafeCache directoryCache)
        {
            this.logger = logger;
            this.httpClient = httpClient;
            this.watcherOptions = watcherOptions.Value;
            this.directoryCache = directoryCache;
        }
        
        public virtual bool IsLANService()
        {
            return false;
        }

        /// <summary>
        /// WebDAV will return the following:
        /// 
        /// 404: WatchPath to parent directory does not exist.
        /// 201: Uploaded.
        /// 204: Uploaded.
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<UploadResult> UploadStreamAsFile(Stream stream, string remotePath)
        {
//            this.logger.LogInformation($"Directory cache size: {this.directoryCache.GetCacheSize()}");
            
            var streamContent = new StreamContent(stream);

            string uriResource = $"{this.httpClient.BaseAddress}/remote.php/dav/files/{this.watcherOptions.RemoteRootFolder}/{remotePath}";
            var request = new HttpRequestMessage();

            request.RequestUri = new Uri(uriResource);
            request.Method = HttpMethod.Put;
            request.Content = streamContent;

            HttpResponseMessage response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return UploadResult.PathDoesNotExist;
            }

            if (response.StatusCode == HttpStatusCode.NoContent || response.StatusCode == HttpStatusCode.Created)
            {
                return UploadResult.Uploaded;
            }

            throw new WebDavException(
                $"Unknown upload file {remotePath} response status code {response.StatusCode} with response {response.Content.ReadAsStringAsync()}");
        }

        /// <summary>
        /// WebDAV will return the following:
        /// 
        /// 404: WatchPath to directory does not exist.
        /// 207: Directory exists.
        /// 
        /// </summary>
        /// <param name="remoteDirectory"></param>
        /// <returns></returns>
        public async Task<CheckDirectoryResult> CheckDirectoryExists(string remoteDirectory)
        {
            // Make sure we don't try to get remote root path in cache. 
            if (string.IsNullOrEmpty(remoteDirectory) == false && this.directoryCache.IsDirectoryInCache(remoteDirectory))
            {
                return CheckDirectoryResult.DirectoryExists;
            }
            
//            this.logger.LogInformation($"Directory cache size: {this.directoryCache.GetCacheSize()}");
            
            var request = new HttpRequestMessage();

            // Do this to make sure we can work with Windows...?
            string remoteRootFolder = this.watcherOptions.RemoteRootFolder.Replace(Path.DirectorySeparatorChar, '/');

            request.RequestUri = new Uri($"{this.httpClient.BaseAddress}/remote.php/dav/files/{remoteRootFolder}/{remoteDirectory}");
            request.Method = new HttpMethod("PROPFIND");
            
            HttpResponseMessage response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return CheckDirectoryResult.DoesNotExist;
            }

            if (response.StatusCode == HttpStatusCode.MultiStatus)
            {
                this.directoryCache.AddDirectoryToCache(remoteDirectory);
                return CheckDirectoryResult.DirectoryExists;
            }
            
            throw new WebDavException(
                $"Unknown check directory {remoteDirectory} response status code {response.StatusCode} with response {response.Content.ReadAsStringAsync()}");
        }

        public async Task CreateRecursiveRemotePath(string relativeRemotePath)
        {
            if (string.IsNullOrEmpty(relativeRemotePath))
            {
                return;
            }
            
            string parent = Path.GetDirectoryName(relativeRemotePath);

            await this.CreateRecursiveRemotePath(parent);

            if (await this.CheckDirectoryExists(relativeRemotePath) == CheckDirectoryResult.DirectoryExists)
            {
                return;
            }

            CreateDirectoryResult result = await this.CreateDirectory(relativeRemotePath);

            if (result == CreateDirectoryResult.Created || result == CreateDirectoryResult.DirectoryAlreadyExists)
            {
                return;
            }

            throw new WebDavException($"Failed to recursively create directory {relativeRemotePath} with result {result}");
        }
        
        /// <summary>
        /// WebDAV will return the following:
        /// 
        /// 405: Directory already exists.
        /// 409: WatchPath to parent directory does not exist.
        /// 201: Created.
        /// 
        /// </summary>
        /// <param name="remoteDirectory"></param>
        /// <returns></returns>
        private async Task<CreateDirectoryResult> CreateDirectory(string remoteDirectory)
        {
//            this.logger.LogInformation($"Directory cache size: {this.directoryCache.GetCacheSize()}");

            var request = new HttpRequestMessage();

            request.RequestUri = new Uri($"{this.httpClient.BaseAddress}/remote.php/dav/files/{this.watcherOptions.RemoteRootFolder}/{remoteDirectory}");
            request.Method = new HttpMethod("MKCOL");
            
            HttpResponseMessage response = await this.httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);

            if (response.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                this.directoryCache.AddDirectoryToCache(remoteDirectory);
                return CreateDirectoryResult.DirectoryAlreadyExists;
            }

            if (response.StatusCode == HttpStatusCode.Conflict)
            {
                return CreateDirectoryResult.PathDoesNotExist;
            }

            if (response.StatusCode == HttpStatusCode.Created)
            {
                this.directoryCache.AddDirectoryToCache(remoteDirectory);
                return CreateDirectoryResult.Created;
            }
         
            throw new WebDavException(
                $"Unknown create remote directory {remoteDirectory} response status code {response.StatusCode} with response {response.Content.ReadAsStringAsync()}");
        }
    }
}