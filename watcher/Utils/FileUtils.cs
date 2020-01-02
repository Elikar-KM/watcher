namespace watcher.Utils
{
    using System.IO.Abstractions;

    public static class FileUtils
    {
        public static PathType GetPathType(this IFileSystem fileSystem, string path)
        {
            if (fileSystem.File.Exists(path))
            {
                return PathType.File;
            }

            if (fileSystem.Directory.Exists(path))
            {
                return PathType.Directory;
            }

            return PathType.NonExisting;
        }
    }
}