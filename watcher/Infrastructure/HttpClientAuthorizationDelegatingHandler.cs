namespace watcher.Infrastructure
{
    using System;
    using System.Collections.Generic;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    public class HttpClientAuthorizationDelegatingHandler : DelegatingHandler
    {
        private readonly WebDAVAuthToken authToken;

        public HttpClientAuthorizationDelegatingHandler(WebDAVAuthToken authToken)
        {
            this.authToken = authToken;
        }
        
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Add("Authorization", new List<string>());
            
            var byteArray = Encoding.ASCII.GetBytes(this.authToken.AuthToken);
            
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));

            return await base.SendAsync(request, cancellationToken);
        }
    }
}