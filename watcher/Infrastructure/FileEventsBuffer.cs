namespace watcher.Infrastructure
{
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using watcher.Options;

    public class FileEventsBuffer : IFileEventsBuffer
    {
        private readonly ReaderWriterLockSlim rwLock = new ReaderWriterLockSlim();
        private readonly ILogger<FileEventsBuffer> logger;
        private readonly WatcherOptions watcherOptions;

        private ConcurrentBag<FileSystemEventArgs> eventsBuffer = new ConcurrentBag<FileSystemEventArgs>();

        public FileEventsBuffer(ILogger<FileEventsBuffer> logger, IOptions<WatcherOptions> watcherOptions)
        {
            this.logger = logger;
            this.watcherOptions = watcherOptions.Value;
        }
        
        public void AddEvent(FileSystemEventArgs ev)
        {
            // TODO: We need to check for directory rename events. We need to recursively get all the files / directories in case of rename. 
            try
            {
                bool acquiredLock = this.rwLock.TryEnterReadLock(this.watcherOptions.EventsBufferLockTimeoutMs);

                if (acquiredLock == false)
                {
                    this.logger.LogError("OnChanged failed to acquire write lock...");
                    return;
                }
                
                this.eventsBuffer.Add(ev);
            }
            finally
            {
                this.rwLock.ExitReadLock();
            }
        }

        public IEnumerable<FileSystemEventArgs> FlushEvents()
        {
            ConcurrentBag<FileSystemEventArgs> evsToReturn;
            
            try
            {
                bool acquiredLock = this.rwLock.TryEnterWriteLock(this.watcherOptions.EventsBufferLockTimeoutMs);

                if (acquiredLock == false)
                {
                    this.logger.LogError(
                        $"SyncToDatabase failed to acquire write lock after {this.watcherOptions.EventsBufferLockTimeoutMs}ms...");
                    
                    return new List<FileSystemEventArgs>();
                }

                evsToReturn = this.eventsBuffer;

                this.eventsBuffer = new ConcurrentBag<FileSystemEventArgs>();
            }
            finally
            {
                this.rwLock.ExitWriteLock();
            }

            return evsToReturn;
        }
    }
}