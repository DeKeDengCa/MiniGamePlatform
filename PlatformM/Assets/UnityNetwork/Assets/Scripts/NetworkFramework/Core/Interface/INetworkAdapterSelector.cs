using NetworkFramework.Core.Model;

namespace NetworkFramework.Core.Interface
{
    public interface INetworkAdapterSelector
    {
        INetworkAdapter SelectAdapter(Request request);
    }
}