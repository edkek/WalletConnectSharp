using WalletConnectSharp.Auth.Interfaces;
using WalletConnectSharp.Auth.Models;
using WalletConnectSharp.Common.Model.Errors;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Core;
using WalletConnectSharp.Core.Interfaces;

namespace WalletConnectSharp.Auth.Controllers;

public partial class AuthEngine : IAuthEngine
{
    public const long MinExpiry = Clock.FIVE_MINUTES;
    public const long MaxExpiry = Clock.SEVEN_DAYS;
    
    internal bool IsValidRequest(RequestParams @params)
    {
        var validAudience = Utils.IsValidUrl(@params.Aud);
        // TODO: From typescript
        // FIXME: disabling this temporarily since it's failing expected values like `chainId: "1"`
        // const validChainId = isValidChainId(params.chainId);
        var domainInAud = @params.Aud.Contains(@params.Domain);
        var hasNonce = !string.IsNullOrWhiteSpace(@params.Nonce);
        var hasValidType = @params.Type == "eip4361";
        var expiry = @params.Expiry;
        if (expiry != null && !Utils.IsValidRequestExpiry(expiry.Value, MinExpiry, MaxExpiry))
        {
            throw new ArgumentException($"Request expiry: {expiry}. Expiry must be a number (in seconds) between {MinExpiry} and {MaxExpiry}");
        }

        return validAudience && domainInAud && hasNonce && hasValidType;
    }

    internal PendingRequest GetPendingRequest(IStore<long, Message> pendingResponses, long id)
    {
        return pendingResponses.Values.OfType<PendingRequest>().FirstOrDefault(request => request.Id == id);
    }

    internal bool IsValidRespond(Message @params, IStore<long, Message> pendingResponses)
    {
        if (@params.Id == null)
            return false;
        
        var validId = GetPendingRequest(pendingResponses, (long)@params.Id);

        return validId != null;
    }
}
