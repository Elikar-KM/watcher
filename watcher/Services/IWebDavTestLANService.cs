namespace watcher.Services
{
    using System.Threading.Tasks;

    public interface IWebDavTestLANService
    {
        Task<bool> IsLANWebDavHostAvailable();
    }
}