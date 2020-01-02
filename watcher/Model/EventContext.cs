namespace watcher.Model
{
    using Microsoft.EntityFrameworkCore;

    public class EventContext : DbContext
    {
        public DbSet<FileSystemEvent> Events { get; set; }

        public EventContext(DbContextOptions<EventContext> options) : base(options)
        {
        }
    }
}