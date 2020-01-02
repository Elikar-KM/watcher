namespace watcher.Services.WebDAV
{
    using System;

    public class WebDavException : Exception
    {
        public WebDavException(string msg) : base(msg)
        {
        }
    }
}