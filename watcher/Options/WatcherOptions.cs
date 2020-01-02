namespace watcher.Options
{
    using System.Collections.Generic;

    public class WatcherOptions
    {
        // TODO: Split Database configs to DatabaseOptions class...?
        public int UnprocessedBatchCount { get; set; }
        
        public int DatabaseRetryLimit { get; set; }
        
        public int DatabaseRetrySleepPeriodMs { get; set; }
        
        public int NumParallelConnections { get; set; }
        
        public int PerConnectionEventsBatchCount { get; set; }
        
        public Dictionary<string, string> RemapRemotePatterns { get; set; }
        
        public int EventsBufferLockTimeoutMs { get; set; }
        
        public string WatchPath { get; set; }
        
        public int InternalBufferSize { get; set; }
        
        public string RemoteRootFolder { get; set; }
        
        public long MaxFileBytesSizeWithoutLAN { get; set; }
        
        public string WatcherConfFilePath { get; set; }
    }
}