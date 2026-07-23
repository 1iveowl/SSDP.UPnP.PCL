using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SSDP.UPnP.PCL.Helper
{
    internal static class HeaderHelper
    {
        internal static void AddOptionalHeader(StringBuilder stringBuilder, string name, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                stringBuilder.Append($"{name}: {value}\r\n");
            }
        }

        internal static IDictionary<string, string> SingleOutAdditionalHeaders(
            IEnumerable<string> defaultHeaders,
            IReadOnlyDictionary<string, string> headers)
        {
            var defaults = new HashSet<string>(defaultHeaders, StringComparer.OrdinalIgnoreCase);

            return headers
                .Where(header => !defaults.Contains(header.Key))
                .ToDictionary(header => header.Key, header => header.Value, StringComparer.OrdinalIgnoreCase);
        }
    }
}
