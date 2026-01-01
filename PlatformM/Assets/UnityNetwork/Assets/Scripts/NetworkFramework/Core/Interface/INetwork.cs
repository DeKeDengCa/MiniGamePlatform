using System.Threading;
using System.Threading.Tasks;
using NetworkFramework.Core.Model;

namespace NetworkFramework.Core.Interface
{
    public interface INetwork
    {
        Task<Response>  Request(Request request, CancellationToken token);
        Task<Response>  Connect(string wsUrl, CancellationToken token);
        void  Disconnect(string wsUrl);
    }
}