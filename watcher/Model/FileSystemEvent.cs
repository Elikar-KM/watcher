namespace watcher.Model
{
    using System.ComponentModel.DataAnnotations.Schema;

    public class FileSystemEvent
    {
        [Column("id_timestamp")]
        public long Id { get; set; }
        
        [Column("file_path")]
        public string FilePath { get; set; }
        
        [Column("processed")]
        public bool Processed { get; set; }
        
        [Column("created_timestamp")]
        public long CreatedTimestamp { get; set; }
        
        [Column("processed_timestamp")]
        public long? ProcessedTimestamp { get; set; }
    }
}