namespace watcher.Infrastructure
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;

    public class Http2UpgraderDelegatingHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Version = new Version(2, 0);

            return await base.SendAsync(request, cancellationToken);
        }
    }
}