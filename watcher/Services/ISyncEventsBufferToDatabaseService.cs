namespace watcher.Services
{
    using System.Collections.Generic;
    using System.IO;
    using System.Threading.Tasks;
    using Coravel.Invocable;

    public interface ISyncEventsBufferToDatabaseService : IInvocable
    {
        Task SyncEventsToDatabase(IEnumerable<FileSystemEventArgs> evs);
    }
}