using WalletConnectSharp.Auth.Models;
using WalletConnectSharp.Common;
using WalletConnectSharp.Core;
using WalletConnectSharp.Core.Interfaces;

namespace WalletConnectSharp.Auth.Interfaces;

[Obsolete("WalletConnectSharp is now considered deprecated and will reach End-of-Life on February 17th 2025. For more details, including migration guides please see: https://docs.reown.com")]
public interface IAuthClient : IModule, IAuthClientEvents
{
    string Protocol { get; }
    int Version { get; }
    
    ICore Core { get; set; }
    Metadata Metadata { get; set; }
    string ProjectId { get; set; }
    IStore<string, AuthData> AuthKeys { get; set; }
    IStore<string, PairingData> PairingTopics { get; set; }
    IStore<long, Message> Requests { get; set; }

    IAuthEngine Engine { get; }
    
    AuthOptions Options { get; }
    
    IDictionary<long, PendingRequest> PendingRequests { get; }
    
    Task<RequestUri> Request(RequestParams @params, string topic = null);

    Task Respond(Message message, string iss);

    Task<IJsonRpcHistory<WcAuthRequest, Cacao>> AuthHistory();

    string FormatMessage(Cacao.CacaoPayload cacao);
    
    string FormatMessage(Cacao.CacaoRequestPayload cacao, string iss);
    
    internal bool OnAuthRequest(AuthRequest request);

    internal bool OnAuthResponse(AuthErrorResponse errorResponse);

    internal bool OnAuthResponse(AuthResponse response);
}
