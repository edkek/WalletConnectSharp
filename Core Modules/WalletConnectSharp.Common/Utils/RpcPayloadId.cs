using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;

namespace WalletConnectSharp.Common.Utils;

/// <summary>
/// A static class that can generate random JSONRPC ids using the current time as a source of randomness 
/// </summary>
public static class RpcPayloadId
{
    private static readonly Random rng = new Random();
    private static readonly DateTime UnixEpoch =
        new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        
    /// <summary>
    /// Generate a new random JSON-RPC id. The clock is used as a source of randomness
    /// </summary>
    /// <returns>A random JSON-RPC id</returns>
    public static long Generate()
    {
        var date = (long)((DateTime.UtcNow - UnixEpoch).TotalMilliseconds) * (10L * 10L * 10L);
        var extra = (long)Math.Floor(rng.NextDouble() * (10.0 * 10.0 * 10.0));
        return date + extra;
    }

    public static long GenerateFromDataHash(object data)
    {
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(data)));

        // Take the first 6 bytes to stay within JavaScript's safe integer range
        long id = 0;
        for (var i = 0; i < 6; i++)
        {
            id = (id << 8) | hash[i];
        }

        // Ensure the id is positive
        id &= 0x7FFFFFFFFFFFFF;

        return id;
    }
}
