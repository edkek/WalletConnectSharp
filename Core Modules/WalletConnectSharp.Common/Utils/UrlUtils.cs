using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace WalletConnectSharp.Common.Utils
{
    /// <summary>
    /// A helper class for URLs
    /// </summary>
    public static class UrlUtils
    {
        /// <summary>
        /// Parse query strings encoded parameters and return a dictionary
        /// </summary>
        /// <param name="queryString">The query string to parse</param>
        public static Dictionary<string, string> ParseQs(string queryString)
        {
            return Regex
                .Matches(queryString, "([^?=&]+)(=([^&]*))?", RegexOptions.None, TimeSpan.FromMilliseconds(100))
                .ToDictionary(x => x.Groups[1].Value, x => x.Groups[3].Value);
        }

        /// <summary>
        /// Convert a dictionary to a query string
        /// </summary>
        /// <param name="params">A dictionary to convert to a query string</param>
        /// <returns>A query string containing all parameters from the dictionary</returns>
        public static string StringifyQs(Dictionary<string, string> @params)
        {
            var sb = new StringBuilder();
            foreach (var kvp in @params)
            {
                if (kvp.Value == null)
                {
                    continue;
                }

                sb.Append(sb.Length == 0 ? "?" : "&");
                sb.Append($"{WebUtility.UrlEncode(kvp.Key)}={WebUtility.UrlEncode(kvp.Value)}");
            }

            return sb.ToString();
        }
    }
}
