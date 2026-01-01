using System.Threading;
using System.Threading.Tasks;
using NetworkFramework.Core.Model;

namespace NetworkFramework.Core.Interface
{
    public interface INetworkAdapter
    {
        Task<Response> Request(Request request, CancellationToken token);
    }
}