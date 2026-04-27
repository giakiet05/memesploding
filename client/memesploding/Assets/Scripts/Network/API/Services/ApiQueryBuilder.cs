using System;
using System.Collections.Generic;
using System.Linq;

namespace Network.API.Services
{
    public static class ApiQueryBuilder
    {
        public static string Build(string url, Dictionary<string, string> query)
        {
            if (query == null || query.Count == 0)
                return url;

            var parts = query
                .Where(kv => !string.IsNullOrWhiteSpace(kv.Value))
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}")
                .ToList();

            if (parts.Count == 0)
                return url;

            var separator = url.Contains("?") ? "&" : "?";
            return $"{url}{separator}{string.Join("&", parts)}";
        }
    }
}
