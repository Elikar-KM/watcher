namespace watcher.Services
{
    using System.IO;
    using System.Threading.Tasks;
    using watcher.Services.WebDAV;

    public interface IWebDavService
    {
        // Indicate LAN service for large file uploads.
        bool IsLANService();

        Task<UploadResult> UploadStreamAsFile(Stream stream, string remotePath);

        Task<CheckDirectoryResult> CheckDirectoryExists(string remoteDirectory);
        
        Task CreateRecursiveRemotePath(string relativeRemotePath);
    }
}