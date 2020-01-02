namespace watcher
{
    using System;
    using System.Data.SQLite;
    using System.IO;
    using System.IO.Abstractions;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using Coravel;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Hosting;
    using Serilog;
    using watcher.Infrastructure;
    using watcher.Model;
    using watcher.Options;
    using watcher.Services;

    public class Program
    {
        public static void Main(string[] args)
        {
            const string mutexName = @"Global\watcher";

            var mutex = new Mutex(true, mutexName, out var createdNew);

            if (!createdNew)
            {
                return;
            }

            IConfiguration configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            IHost host = CreateHostBuilder(args).Build();

            host.Services.UseScheduler(scheduler =>
            {
                scheduler.Schedule<ISyncEventsBufferToDatabaseService>()
                    .EverySeconds(int.Parse(configuration["Watcher:EventsSyncToDbPeriodSeconds"]))
                    .PreventOverlapping(typeof(ISyncEventsBufferToDatabaseService).FullName);

                scheduler.Schedule<IProcessEventsService>()
                    .EverySeconds(int.Parse(configuration["Watcher:ProcessSleepTimeSeconds"]))
                    .PreventOverlapping(typeof(IProcessEventsService).FullName);
            });
            
            host.Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .UseSerilog()
                .ConfigureServices((host, services) =>
                {
                    IConfiguration watcherConf = host.Configuration.GetSection("Watcher");

                    services.Configure<WatcherOptions>(host.Configuration.GetSection("Watcher"));

                    string dbConn = watcherConf["Database"];

                    string localWatcherConfPath = watcherConf["WatcherConfFilePath"];

                    string localWatcherConfDir = Path.GetDirectoryName(localWatcherConfPath);
                    
                    string localWatcherConfFilename = Path.GetFileName(localWatcherConfPath);
                    
                    IConfiguration localWatcherConf = new ConfigurationBuilder()
                        .SetBasePath(localWatcherConfDir)
                        .AddJsonFile(localWatcherConfFilename)
                        .Build();

                    // Set the Regex cache to be twice the number of filter patterns to be safe.
                    Regex.CacheSize =  Math.Max(
                        Regex.CacheSize,
                        2 * localWatcherConf.GetSection("RejectFilterPatterns").AsEnumerable().Count());

                    services.AddSingleton<IDirectoryThreadSafeCache, DirectoryCache>();

                    services.AddScoped<IEventRepository, EventRepository>();

                    services.AddDbContext<EventContext>(options => options.UseSqlite(new SQLiteConnection(dbConn)));

                    services.AddTransient<ISyncEventsBufferToDatabaseService, SyncEventsBufferToDatabaseService>();
                    services.AddTransient<IProcessEventsService, ProcessEventsService>();
                    
                    services.AddSingleton<IHandleFileChangeService, FileSystemWatcherService>();

                    services.AddSingleton<IFileEventsBuffer, FileEventsBuffer>();
                    
                    services.AddScheduler();
                    
                    services.AddHostedService<FileSystemWatcherService>();

                    services.AddScoped<HttpClientAuthorizationDelegatingHandler>();
                    services.AddScoped<Http2UpgraderDelegatingHandler>();

                    services.AddSingleton(provider => new WebDAVAuthToken(localWatcherConf["WebDAV:Auth"]));

                    // This is a work-around until https://github.com/aspnet/Extensions/issues/2077 is merged.
                    // Order of registration of delegating handler matters!
                    services.AddHttpClient<WebDavService>()
                        .ConfigureHttpClient(cl =>
                        {
                            cl.BaseAddress = new Uri(localWatcherConf["WebDAV:Host"]);
                            cl.Timeout = TimeSpan.FromMilliseconds(int.Parse(watcherConf["NetworkTimeoutMs"]));
                        })
                        .AddHttpMessageHandler<Http2UpgraderDelegatingHandler>()
                        .AddHttpMessageHandler<HttpClientAuthorizationDelegatingHandler>()
                        .SetHandlerLifetime(TimeSpan.FromMinutes(30));  //Default lifetime is 2 minutes

                    // Testing to see if LAN WebDAV service is available.
                    services.AddHttpClient<WebDavTestLANService>()
                        .ConfigureHttpClient(cl =>
                        {
                            cl.BaseAddress = new Uri(localWatcherConf["WebDAV:HostLAN"]);
                            cl.Timeout = TimeSpan.FromMilliseconds(int.Parse(watcherConf["NetworkTimeoutMsTestLAN"]));
                        })
                        .AddHttpMessageHandler<Http2UpgraderDelegatingHandler>()
                        .AddHttpMessageHandler<HttpClientAuthorizationDelegatingHandler>();

                    // Add a LAN WebDAV service for large file uploads.
                    services.AddHttpClient<WebDavLANService>()
                        .ConfigureHttpClient(cl =>
                        {
                            cl.BaseAddress = new Uri(localWatcherConf["WebDAV:HostLAN"]);
                            cl.Timeout = TimeSpan.FromMilliseconds(int.Parse(watcherConf["NetworkTimeoutMsLAN"]));
                        })
                        .AddHttpMessageHandler<Http2UpgraderDelegatingHandler>()
                        .AddHttpMessageHandler<HttpClientAuthorizationDelegatingHandler>()
                        .SetHandlerLifetime(TimeSpan.FromMinutes(30));

                    services.AddScoped<IWebDavService>(provider => provider.GetRequiredService<WebDavService>());
                    services.AddScoped<IWebDavService>(provider => provider.GetService<WebDavLANService>());
                    services.AddScoped<IWebDavTestLANService>(provider => provider.GetRequiredService<WebDavTestLANService>());

                    services.AddSingleton<IFileSystemWatcherFactory, FileSystemWatcherFactory>();
                    services.AddSingleton<IFileSystem, FileSystem>();
                });
    }
}
