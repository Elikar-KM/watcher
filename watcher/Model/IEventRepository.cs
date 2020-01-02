namespace watcher.Model
{
    using System.Collections.Generic;
    using System.Threading.Tasks;

    public interface IEventRepository
    {
        IAsyncEnumerable<FileSystemEvent> GetUnprocessedEventsEarliest(int unprocessedBatchCount);

        Task InsertEventsAsync(ICollection<FileSystemEvent> events);

        Task UpdateEventsProcessStatusAsync(ICollection<FileSystemEvent> fsEvents);
    }
}