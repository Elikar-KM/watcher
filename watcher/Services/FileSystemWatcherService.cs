namespace watcher.Services
{
    using System.IO;
    using System.IO.Abstractions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Hosting;
    using Microsoft.Extensions.Options;
    using watcher.Infrastructure;
    using watcher.Options;

    public class FileSystemWatcherService : BackgroundService, IHandleFileChangeService
    {
        private readonly IFileSystemWatcherFactory fileSystemWatcherFactory;
        private readonly WatcherOptions watcherOptions;
        private readonly IFileEventsBuffer fileEventsBuffer;

        private IFileSystemWatcher watcher;

        public FileSystemWatcherService(
            IOptions<WatcherOptions> watcherOptions,
            IFileSystemWatcherFactory fileSystemWatcherFactory,
            IFileEventsBuffer fileEventsBuffer)
        {
            this.watcherOptions = watcherOptions.Value;
            this.fileSystemWatcherFactory = fileSystemWatcherFactory;
            this.fileEventsBuffer = fileEventsBuffer;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            this.watcher = this.fileSystemWatcherFactory.CreateNew();

            this.watcher.Path = this.watcherOptions.WatchPath;
            this.watcher.InternalBufferSize = this.watcherOptions.InternalBufferSize;

            // Watch for changes in LastAccess and LastWrite times, and
            // the renaming of files or directories.
            this.watcher.NotifyFilter = NotifyFilters.LastWrite
                                   | NotifyFilters.FileName
                                   | NotifyFilters.DirectoryName;

            // Add event handlers.
            this.watcher.Changed += this.OnChanged;
            this.watcher.Created += this.OnChanged;
            this.watcher.Deleted += this.OnDeleted;
            this.watcher.Renamed += this.OnChanged;
            
            this.watcher.IncludeSubdirectories = true;

            this.watcher.EnableRaisingEvents = true;
            
            return Task.CompletedTask;
        }
        
        public void OnChanged(object source, FileSystemEventArgs e)
        {
            this.fileEventsBuffer.AddEvent(e);
        }
        
        public void OnDeleted(object sender, FileSystemEventArgs e)
        {
            // TODO: Should we even do this...? Just never delete on server side...
        }
    }
}