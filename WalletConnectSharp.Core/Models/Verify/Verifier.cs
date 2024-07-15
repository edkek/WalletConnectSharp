using Newtonsoft.Json;
using WalletConnectSharp.Common.Logging;

namespace WalletConnectSharp.Core.Models.Verify;

public sealed class Verifier : IDisposable
{
    private const string VerifyServer = "https://verify.walletconnect.com";

    private readonly HttpClient _client = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    public async Task<string> Resolve(string attestationId)
    {
        try
        {
            var url = $"{VerifyServer}/attestation/{attestationId}";
            var results = await _client.GetStringAsync(url);

            var verifiedContext = JsonConvert.DeserializeObject<VerifiedContext>(results);

            return verifiedContext != null ? verifiedContext.Origin : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    public void Dispose()
    {
        _client?.Dispose();
    }
}
