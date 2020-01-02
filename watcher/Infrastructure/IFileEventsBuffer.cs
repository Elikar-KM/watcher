namespace watcher.Infrastructure
{
    using System.Collections.Generic;
    using System.IO;

    public interface IFileEventsBuffer
    {
        void AddEvent(FileSystemEventArgs eventArgs);

        /// <summary>
        /// Flush and return all events from memory. This will clear out the events held by this buffer.
        /// </summary>
        /// <returns></returns>
        IEnumerable<FileSystemEventArgs> FlushEvents();
    }
}