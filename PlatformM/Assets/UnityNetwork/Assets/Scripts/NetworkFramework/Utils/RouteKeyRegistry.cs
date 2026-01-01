using System.Collections.Concurrent;
using Scommon;

namespace NetworkFramework.Utils
{
    public static class RouteKeyRegistry
    {
        private static readonly ConcurrentDictionary<string, string> Map = new ConcurrentDictionary<string, string>();

        public static void UpdateFromControl(RspNotifyControl ctrl)
        {
            if (ctrl == null) return;
            if (!ctrl.UpdateRouteKey) return;

            var serviceName = ctrl.RouteService ?? string.Empty;
            var routeKey = ctrl.RouteKey ?? string.Empty;
            if (string.IsNullOrEmpty(serviceName)) return;

            if (!string.IsNullOrEmpty(routeKey))
                Map.AddOrUpdate(serviceName, routeKey, (_, __) => routeKey);
            else
                Map.TryRemove(serviceName, out _);
        }

        public static bool TryGetRouteKey(string serviceName, out string routeKey)
        {
            routeKey = null;
            if (string.IsNullOrEmpty(serviceName)) return false;
            return Map.TryGetValue(serviceName, out routeKey);
        }

        public static void Clear()
        {
            Map.Clear();
        }
    }
}