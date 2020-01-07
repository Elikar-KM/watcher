namespace watcher.Model
{
    using System;
    using System.Collections.Generic;
    using System.Data.SQLite;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.EntityFrameworkCore.Storage;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;
    using watcher.Options;

    public class EventRepository : IEventRepository
    {
        private readonly EventContext eventContext;
        private readonly WatcherOptions watcherOptions;
        private readonly ILogger<EventRepository> logger;

        private readonly string updateEventsProcessStatusCommand = @"
        UPDATE events
        SET processed           = @processedValue,
            processed_timestamp = @processedTimestamp
        WHERE file_path = (SELECT file_path FROM events WHERE id_timestamp = @eventId)
          AND id_timestamp IN (SELECT id_timestamp
                               FROM events
                               WHERE file_path = (SELECT file_path FROM events WHERE id_timestamp = @eventId)
                                 AND processed = 0
                                 AND id_timestamp <= @eventId);
        ";

        private readonly string unprocessedEventsQuery = @"
        SELECT id_timestamp        AS id_timestamp,
               events.file_path    AS file_path,
               processed           AS processed,
               flags               AS flags,
               created_timestamp   AS created_timestamp,
               processed_timestamp AS processed_timestamp
        FROM events
                 JOIN (
            SELECT file_path              AS file_path,
                   MAX(created_timestamp) AS latest_timestamp
            FROM events
            WHERE processed = 0
            GROUP BY file_path) AS file_path_latest
                      ON events.file_path = file_path_latest.file_path AND
                         events.created_timestamp = file_path_latest.latest_timestamp
        WHERE processed = 0
        ORDER BY id_timestamp
        LIMIT @unprocessedBatchCount;
        ";

        public EventRepository(ILogger<EventRepository> logger, EventContext eventContext, IOptions<WatcherOptions> watcherOptions)
        {
            this.logger = logger;
            this.eventContext = eventContext;
            this.watcherOptions = watcherOptions.Value;
        }

        public IAsyncEnumerable<FileSystemEvent> GetUnprocessedEventsEarliest(int unprocessedBatchCount)
        {
            return this.eventContext.Events.FromSqlRaw(
                    this.unprocessedEventsQuery,
                    new SQLiteParameter("unprocessedBatchCount", unprocessedBatchCount))
                .AsAsyncEnumerable();
        }
        
        public async Task InsertEventsAsync(ICollection<FileSystemEvent> events)
        {
            for (int attempt = 0; attempt < this.watcherOptions.DatabaseRetryLimit; attempt += 1)
            {
                try
                {
                    this.logger.LogInformation($"Inserting {events.Count} events with first path = {events.First().FilePath}");

                    await using IDbContextTransaction transaction = await this.eventContext.Database.BeginTransactionAsync();

                    await this.eventContext.Events.AddRangeAsync(events);

                    await this.eventContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    this.logger.LogInformation($"Successfully inserted {events.Count}");

                    break;
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"Attempt #{attempt} to insert {events.Count} events failed with exception {ex.GetType()} {ex.Message}: {ex.StackTrace}");

                    if (attempt == this.watcherOptions.DatabaseRetryLimit - 1)
                    {
                        this.logger.LogError($"Reached maximum {this.watcherOptions.DatabaseRetryLimit} retries for database insert events, bailing out...");
                        throw;
                    }
                }
                
                Thread.Sleep(TimeSpan.FromMilliseconds(this.watcherOptions.DatabaseRetrySleepPeriodMs));
            }
        }

        public async Task UpdateEventsProcessStatusAsync(ICollection<FileSystemEvent> fsEvents)
        {
            if (fsEvents.Count == 0)
            {
                return;
            }
            
            for (int attempt = 0; attempt < this.watcherOptions.DatabaseRetryLimit; attempt += 1)
            {
                try
                {
                    this.logger.LogInformation($"Updating {fsEvents.Count} events with first path = {fsEvents.First().FilePath}");

                    await using IDbContextTransaction transaction = await this.eventContext.Database.BeginTransactionAsync();

                    foreach (FileSystemEvent fsEvent in fsEvents)
                    {
                        await this.eventContext.Database.ExecuteSqlRawAsync(
                            this.updateEventsProcessStatusCommand,
                            new SQLiteParameter("processedValue", fsEvent.Processed),
                            new SQLiteParameter("processedTimestamp", fsEvent.ProcessedTimestamp),
                            new SQLiteParameter("eventId", fsEvent.Id));
                    }

                    await this.eventContext.SaveChangesAsync();

                    await transaction.CommitAsync();

                    this.logger.LogInformation($"Successfully updated {fsEvents.Count} events with first path = {fsEvents.First().FilePath}");
                    
                    break;
                }
                catch (Exception ex)
                {
                    this.logger.LogError($"Attempt #{attempt} to update {fsEvents.Count} events failed with exception {ex.GetType()} {ex.Message}: {ex.StackTrace}");

                    if (attempt == this.watcherOptions.DatabaseRetryLimit - 1)
                    {
                        this.logger.LogError(
                            $"Reached maximum {this.watcherOptions.DatabaseRetryLimit} retries for database update event, bailing out...");
                        throw;
                    }
                }

                Thread.Sleep(TimeSpan.FromMilliseconds(this.watcherOptions.DatabaseRetrySleepPeriodMs));
            }
        }
    }
}