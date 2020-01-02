namespace watcher.Services
{
    using System.IO;

    public interface IHandleFileChangeService
    {
        void OnChanged(object source, FileSystemEventArgs e);

        void OnDeleted(object sender, FileSystemEventArgs e);
    }
}