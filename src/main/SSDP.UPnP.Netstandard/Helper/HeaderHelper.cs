using System.Collections.Generic;
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

        internal static IDictionary<string, string> SingleOutAdditionalHeaders
            (IEnumerable<string> defaultHeaders,
            IDictionary<string, string> headers)
        {
            foreach (var df in defaultHeaders)
            {
                headers.Remove(df);
            }
            return headers;
        }
    }
}
