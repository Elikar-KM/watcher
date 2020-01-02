namespace watcher.Infrastructure
{
    public class WebDAVAuthToken
    {
        public string AuthToken { get; }

        public WebDAVAuthToken(string authToken)
        {
            this.AuthToken = authToken;
        }
    }
}