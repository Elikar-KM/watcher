namespace watcher.Services
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using watcher.Infrastructure;
    using watcher.Model;
    using watcher.Options;
    using watcher.Utils;

    public class SyncEventsBufferToDatabaseService : ISyncEventsBufferToDatabaseService
    {
        private readonly IFileEventsBuffer fileEventsBuffer;
        private readonly IEventRepository eventRepository;
        private readonly ILogger<SyncEventsBufferToDatabaseService> logger;
        private readonly IFileSystem fileSystem;
        private readonly WatcherOptions watcherOptions;

        public SyncEventsBufferToDatabaseService(
            ILogger<SyncEventsBufferToDatabaseService> logger,
            IFileSystem fileSystem,
            IOptions<WatcherOptions> watcherOptions,
            IEventRepository eventRepository,
            IFileEventsBuffer fileEventsBuffer)
        {
            this.logger = logger;
            this.watcherOptions = watcherOptions.Value;
            this.fileSystem = fileSystem;
            this.eventRepository = eventRepository;
            this.fileEventsBuffer = fileEventsBuffer;
        }
        
        public Task Invoke()
        {
            IEnumerable<FileSystemEventArgs> evs = this.fileEventsBuffer.FlushEvents();
            return this.SyncEventsToDatabase(evs);
        }

        public async Task SyncEventsToDatabase(IEnumerable<FileSystemEventArgs> evs)
        {
            try
            {
                ICollection<FileSystemEventArgs> eventsToFilter = evs.ToList();
                
                if (eventsToFilter.Count == 0)
                {
                    return;
                }

                var jsonStream = this.fileSystem.File.OpenRead(this.watcherOptions.WatcherConfFilePath);

                var filterPatterns = new List<string>();
                
                using (JsonDocument jsonDocument = JsonDocument.Parse(jsonStream))
                {
                    JsonElement filterPatternsJsonElem;
                    
                    bool succeeded = jsonDocument.RootElement.TryGetProperty("RejectFilterPatterns", out filterPatternsJsonElem);

                    if (succeeded)
                    {
                        filterPatterns = JsonSerializer.Deserialize<List<string>>(filterPatternsJsonElem.GetRawText());
                    }
                }
                
                long currentTime = DateTimeOffset.Now.ToUnixTimeSeconds();

                // Do this because FileSystemWatcher can emit multiple events on a single operation,
                // e.g. echo "asda" >> test.txt                 -> two events.
                var emittedPaths = new HashSet<string>();

                ICollection<FileSystemEventArgs> interestingEvents = eventsToFilter
                    .Where(ev => this.IsEventInteresting(ev.FullPath, emittedPaths, filterPatterns)).ToList();

                List<FileSystemEvent> renameEvents = this.InflateRenameEvents(
                    interestingEvents.Where(ev => ev.ChangeType == WatcherChangeTypes.Renamed),
                    filterPatterns,
                    currentTime);

                List<FileSystemEvent> events = interestingEvents
                    .Where(ev => ev.ChangeType != WatcherChangeTypes.Renamed)
                    .Select(ev => new FileSystemEvent
                    {
                        CreatedTimestamp = currentTime,
                        FilePath = ev.FullPath,
                        Processed = false
                    }).ToList();

                events.AddRange(renameEvents);

                if (events.Count > 0)
                {
                    this.logger.LogInformation($"Number of events in buffer: {events.Count}");
                    await this.eventRepository.InsertEventsAsync(events);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogError($"SyncToDatabase exception: {ex.Message} {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Inflates rename events if they are interesting. File rename events are always inflated.
        /// </summary>
        /// <param name="renameEvents"></param>
        /// <param name="rejectFilterPatterns"></param>
        /// <param name="createdTimestamp"></param>
        /// <returns></returns>
        private List<FileSystemEvent> InflateRenameEvents(
            IEnumerable<FileSystemEventArgs> renameEvents,
            ICollection<string> rejectFilterPatterns,
            long createdTimestamp)
        {
            var emittedPaths = new HashSet<string>();

            var inflatedEvents = new List<FileSystemEvent>();

            foreach (FileSystemEventArgs ev in renameEvents)
            {
                if (this.fileSystem.GetPathType(ev.FullPath) == PathType.File)
                {
                    inflatedEvents.Add(new FileSystemEvent
                    {
                        Processed = false,
                        FilePath = ev.FullPath,
                        CreatedTimestamp = createdTimestamp
                    });

                    continue;
                }
                
                if (false == this.IsEventInteresting(ev.FullPath, emittedPaths, rejectFilterPatterns))
                {
                    continue;
                }

                List<FileSystemEvent> interestingEvents = this.WalkDirectory(ev.FullPath)
                    .Where(path => this.IsEventInteresting(path, emittedPaths, rejectFilterPatterns))
                    .Select(path => new FileSystemEvent
                    {
                        Processed = false,
                        FilePath = path,
                        CreatedTimestamp = createdTimestamp
                    })
                    .ToList();
                
                inflatedEvents.AddRange(interestingEvents);
            }
            
            return inflatedEvents;
        }

        private bool IsEventInteresting(
            string eventPath,
            ISet<string> emittedPaths,
            ICollection<string> rejectFilterPatterns)
        {
            if (emittedPaths.Contains(eventPath))
            {
                return false;
            }

            emittedPaths.Add(eventPath);

            foreach (string ignorePattern in rejectFilterPatterns)
            {
                Match match = Regex.Match(eventPath, ignorePattern);

                if (match.Success)
                {
                    return false;
                }
            }

            return true;
        }

        private IEnumerable<string> WalkDirectory(string initPath)
        {
            PathType initPathType = this.fileSystem.GetPathType(initPath);
            
            if (initPathType == PathType.File)
            {
                yield return initPath;
                yield break;
            }

            if (initPathType == PathType.NonExisting)
            {
                yield break;
            }
            
            var stack = new Stack<string>();

            stack.Push(initPath);

            while (stack.Count > 0)
            {
                string currDir = stack.Pop();

                yield return currDir;

                foreach (string childDir in this.fileSystem.Directory.EnumerateDirectories(currDir))
                {
                    stack.Push(childDir);
                }

                foreach (string file in this.fileSystem.Directory.EnumerateFiles(currDir))
                {
                    yield return file;
                }
            }
        }
    }
}