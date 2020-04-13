namespace watcher.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Dasync.Collections;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using watcher.Model;
    using watcher.Options;
    using watcher.Services.WebDAV;
    using watcher.Utils;

    public class ProcessEventsService : IProcessEventsService
    {
        private readonly ILogger<ProcessEventsService> logger;
        private readonly WatcherOptions watcherOptions;
        private readonly IFileSystem fileSystem;
        private readonly IEventRepository eventRepository;
        private readonly IWebDavTestLANService testLanService;
        private readonly ICollection<IWebDavService> webdavServices;
        private readonly IServiceScopeFactory serviceScopeFactory;

        public ProcessEventsService(
            ILogger<ProcessEventsService> logger, 
            IServiceScopeFactory serviceScopeFactory,
            IFileSystem fileSystem,
            IOptions<WatcherOptions> watcherOptions,
            IEventRepository eventRepository,
            IWebDavTestLANService testLanService,
            IEnumerable<IWebDavService> webDavServices)
        {
            this.logger = logger;
            this.serviceScopeFactory = serviceScopeFactory;
            this.fileSystem = fileSystem;
            this.watcherOptions = watcherOptions.Value;
            this.eventRepository = eventRepository;
            this.testLanService = testLanService;
            this.webdavServices = webDavServices.ToList();
        }
        
        public async Task Invoke()
        {
            var eventsBatches = new List<List<FileSystemEvent>>();

            int eventsProcessed = 0;
            int totalEvents = 0;
            
            void IncrementTotalEvents()
            {
                Interlocked.Increment(ref totalEvents);
            }

            void IncrementProcessedEvents()
            {
                Interlocked.Increment(ref eventsProcessed);
            }

            try
            {
                var eventsBatch = new List<FileSystemEvent>();

                IWebDavService webdavServToUse = this.webdavServices.First();
                
                bool isLANAvailable = await this.testLanService.IsLANWebDavHostAvailable();

                string avail = isLANAvailable ? "" : "not ";

                this.logger.LogInformation($"LAN WebDAV is {avail}available");

                webdavServToUse = this.webdavServices.First(webdavServ => webdavServ.IsLANService() == isLANAvailable) 
                                  ?? webdavServToUse;

                await foreach (FileSystemEvent fsEvent in this.eventRepository.GetUnprocessedEventsEarliest(
                    this.watcherOptions.UnprocessedBatchCount))
                {
                    eventsBatch.Add(fsEvent);

                    int threshold = this.watcherOptions.PerConnectionEventsBatchCount;

                    if (eventsBatch.Count >= threshold)
                    {
                        eventsBatches.Add(eventsBatch);

                        eventsBatch = new List<FileSystemEvent>();
                    }
                }

                eventsBatches.Add(eventsBatch);

                await eventsBatches.ParallelForEachAsync(
                    async evBatch =>
                    {
                        await this.ProcessFileSystemEvents(
                            IncrementTotalEvents,
                            IncrementProcessedEvents,
                            evBatch,
                            webdavServToUse
                        );
                    },
                    maxDegreeOfParallelism: this.watcherOptions.NumParallelConnections);
            }
            catch (Exception ex)
            {
                this.logger.LogError($"Caught exception in parallel loop: {ex.GetType()} {ex.Message} {ex.StackTrace}");
            }

            this.logger.LogInformation($"Finished processing {eventsProcessed} out of {totalEvents} events");
        }
        
        private async Task ProcessFileSystemEvents(
            Action incrementTotalEvents, 
            Action incrementProcessedEvents,
            ICollection<FileSystemEvent> fsEvents,
            IWebDavService webdavServiceToUse)
        {
            long processedTimestamp = DateTimeOffset.Now.ToUnixTimeSeconds();
            var processedEvents = new List<FileSystemEvent>();

            IServiceScope scope = this.serviceScopeFactory.CreateScope();

            // Use a new scoped event repository since this should be executed in a separate context (e.g. thread).
            IEventRepository scopedEventRepository = scope.ServiceProvider.GetRequiredService<IEventRepository>();
            
            foreach (FileSystemEvent fsEvent in fsEvents)
            {
                incrementTotalEvents();
                
                string eventPath = fsEvent.FilePath;

                try
                {
                    bool processed;
                    switch (this.fileSystem.GetPathType(eventPath))
                    {
                        case PathType.NonExisting:
                            this.logger.LogInformation($"{eventPath} does not exist, not uploading...");
                            processed = true;
                            break;
                        case PathType.File:
                            processed = await this.ProcessFileEvent(fsEvent, webdavServiceToUse);
                            break;
                        case PathType.Directory:
                            processed = await this.ProcessDirectoryEvent(fsEvent, webdavServiceToUse);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    if (processed)
                    {
                        incrementProcessedEvents();
                        processedEvents.Add(new FileSystemEvent
                        {
                            Id = fsEvent.Id,
                            Processed = true,
                            FilePath = fsEvent.FilePath,
                            CreatedTimestamp = fsEvent.CreatedTimestamp,
                            ProcessedTimestamp = processedTimestamp
                        });
                    }
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"Caught exception in inner loop: {ex.GetType()} {ex.Message} {ex.StackTrace}");
                }
            }

            await scopedEventRepository.UpdateEventsProcessStatusAsync(processedEvents);
        }

        private async Task<bool> ProcessDirectoryEvent(FileSystemEvent fsEvent, IWebDavService webdavServiceToUse)
        {
            string eventPath = fsEvent.FilePath;

            bool created = await this.CreateRemoteDirectoryPath(webdavServiceToUse, eventPath);

            if (created == false)
            {
                return false;
            }
            
            if (await webdavServiceToUse.CheckDirectoryExists(eventPath) == CheckDirectoryResult.DoesNotExist)
            {
                await webdavServiceToUse.CreateRecursiveRemotePath(eventPath);
            }
            
            return true;
        }

        /// <summary>
        /// /
        /// </summary>
        /// <param name="fsEvent"></param>
        /// <param name="webdavServiceToUse"></param>
        /// <returns>Whether the event was processed (e.g. uploaded)</returns>
        private async Task<bool> ProcessFileEvent(FileSystemEvent fsEvent, IWebDavService webdavServiceToUse)
        {
            bool shouldUpload = true;
            
            string eventPath = fsEvent.FilePath;
            
            IFileInfo fileInfo = this.fileSystem.FileInfo.FromFileName(eventPath);
            
            if (fileInfo.Length > this.watcherOptions.MaxFileBytesSizeWithoutLAN)
            {
                if (webdavServiceToUse.IsLANService() == false)
                {
                    shouldUpload = false;
                }
            }

            if (shouldUpload == false)
            {
                return false;
            }
            
            this.logger.LogInformation($"Processing event {fsEvent.Id} with path {fsEvent.FilePath}");
            
            string eventRelativeRemotePath = Path.GetRelativePath(this.watcherOptions.WatchPath, eventPath);

            bool created = await this.CreateRemoteDirectoryPath(webdavServiceToUse, eventPath);

            if (created == false)
            {
                return false;
            }
            
            foreach (string remapPattern in this.watcherOptions.RemapRemotePatterns.Keys)
            {
                if (eventRelativeRemotePath.StartsWith(remapPattern))
                {
                    eventRelativeRemotePath = eventRelativeRemotePath.ReplaceFirst(
                        remapPattern,
                        this.watcherOptions.RemapRemotePatterns[remapPattern]);
                }
            }

            eventRelativeRemotePath = eventRelativeRemotePath.Replace("\\", "_");

            Stream filestream = this.fileSystem.File.OpenRead(eventPath);

            UploadResult result = await webdavServiceToUse.UploadStreamAsFile(filestream, eventRelativeRemotePath);

            this.logger.LogInformation($"Upload result is {result} for {eventPath}");

            return result == UploadResult.Uploaded;
        }

        private async Task<bool> CreateRemoteDirectoryPath(IWebDavService webdavServiceToUse, string eventPath)
        {
            IDirectoryInfo parentDirInfo = this.fileSystem.Directory.GetParent(eventPath);

            if (parentDirInfo.Exists == false)
            {
                this.logger.LogError($"{parentDirInfo.FullName} does not exist, cannot proceed!");
                
                return false;
            }

            // Remove the root path so we get the relative path, which should be the path for remote.
            string relativeRemotePath = Path.GetRelativePath(this.watcherOptions.WatchPath, parentDirInfo.FullName);

            if (await webdavServiceToUse.CheckDirectoryExists(relativeRemotePath) == CheckDirectoryResult.DoesNotExist)
            {
                await webdavServiceToUse.CreateRecursiveRemotePath(relativeRemotePath);
            }
            
            return true;
        }
    }
}