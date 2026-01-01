using System;
using System.Collections.Generic;
using UnityEngine;
using NetworkFramework.Core.Interface;
using NetworkFramework.Core.Model;

namespace NetworkFramework.Core.Adapter
{
    public class DefaultNetworkAdapterSelector : INetworkAdapterSelector
    {
        private readonly INetworkAdapter _httpAdapter;
        private readonly INetworkAdapter _wsAdapter;

        public DefaultNetworkAdapterSelector(INetworkAdapter httpAdapter, INetworkAdapter wsAdapter)
        {
            _httpAdapter = httpAdapter;
            _wsAdapter = wsAdapter;
        }

        public INetworkAdapter SelectAdapter(Request request)
        {
            return request.UseConnectionType switch
            {
                ConnectionType.INCONSTANT => _httpAdapter,
                ConnectionType.PERSISTENT => _wsAdapter,
                _ => throw new InvalidOperationException("Unknown connection type")
            };
        }
    }
}