using WalletConnectSharp.Auth;
using WalletConnectSharp.Auth.Models;
using WalletConnectSharp.Core;
using WalletConnectSharp.Core.Interfaces;
using WalletConnectSharp.Network.Models;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Sign.Models.Engine.Events;
using WalletConnectSharp.Web3Wallet.Controllers;
using WalletConnectSharp.Web3Wallet.Interfaces;

namespace WalletConnectSharp.Web3Wallet;

[Obsolete("WalletConnectSharp is now considered deprecated and will reach End-of-Life on February 17th 2025. For more details, including migration guides please see: https://docs.reown.com")]
public class Web3WalletClient : IWeb3Wallet
{
    public string Name { get; }
    public string Context { get; }

    public event EventHandler<SessionStruct> SessionExpired;
    public event EventHandler<SessionProposalEvent> SessionProposed;
    public event EventHandler<SessionStruct> SessionConnected;
    public event EventHandler<Exception> SessionConnectionErrored;
    public event EventHandler<SessionUpdateEvent> SessionUpdated;
    public event EventHandler<SessionEvent> SessionExtended;
    public event EventHandler<SessionEvent> SessionPinged;
    public event EventHandler<SessionEvent> SessionDeleted;

    public IDictionary<string, SessionStruct> ActiveSessions
    {
        get
        {
            return this.Engine.ActiveSessions;
        }
    }

    public IDictionary<long, ProposalStruct> PendingSessionProposals
    {
        get
        {
            return this.Engine.PendingSessionProposals;
        }
    }

    public PendingRequestStruct[] PendingSessionRequests
    {
        get
        {
            return this.Engine.PendingSessionRequests;
        }
    }

    public IDictionary<long, PendingRequest> PendingAuthRequests
    {
        get
        {
            return this.Engine.PendingAuthRequests;
        }
    }

    public IWeb3WalletEngine Engine { get; }
    public ICore Core { get; }
    public Metadata Metadata { get; }
    
    public static async Task<Web3WalletClient> Init(ICore core, Metadata metadata, string name = null)
    {
        var wallet = new Web3WalletClient(core, metadata, name);
        await wallet.Initialize();

        return wallet;
    }
    
    private Web3WalletClient(ICore core, Metadata metadata, string name = null)
    {
        this.Metadata = metadata;
        if (string.IsNullOrWhiteSpace(this.Metadata.Name))
            this.Metadata.Name = name;
        
        this.Name = string.IsNullOrWhiteSpace(name) ? "Web3Wallet" : name;
        this.Context = $"{Name}-context";
        this.Core = core;
        
        this.Engine = new Web3WalletEngine(this);
        
        WrapEngineEvents();
    }

    private void WrapEngineEvents()
    {
        Engine.SessionExpired += (sender, @struct) => this.SessionExpired?.Invoke(sender, @struct);
        Engine.SessionProposed += (sender, @event) => this.SessionProposed?.Invoke(sender, @event);
        Engine.SessionConnected += (sender, @struct) => this.SessionConnected?.Invoke(sender, @struct);
        Engine.SessionConnectionErrored +=
            (sender, exception) => this.SessionConnectionErrored?.Invoke(sender, exception);
        Engine.SessionUpdated += (sender, @event) => this.SessionUpdated?.Invoke(sender, @event);
        Engine.SessionExtended += (sender, @event) => this.SessionExtended?.Invoke(sender, @event);
        Engine.SessionPinged += (sender, @event) => this.SessionPinged?.Invoke(sender, @event);
        Engine.SessionDeleted += (sender, @event) => this.SessionDeleted?.Invoke(sender, @event);
    }
    
    public Task Pair(string uri, bool activatePairing = false)
    {
        return this.Engine.Pair(uri, activatePairing);
    }

    public Task<SessionStruct> ApproveSession(long id, Namespaces namespaces, string relayProtocol = null)
    {
        return this.Engine.ApproveSession(id, namespaces, relayProtocol);
    }

    public Task<SessionStruct> ApproveSession(ProposalStruct proposal, params string[] approvedAddresses)
    {
        return this.Engine.ApproveSession(proposal, approvedAddresses);
    }

    public Task RejectSession(long id, Error reason)
    {
        return this.Engine.RejectSession(id, reason);
    }

    public Task RejectSession(ProposalStruct proposal, Error reason)
    {
        return this.Engine.RejectSession(proposal, reason);
    }

    public Task RejectSession(ProposalStruct proposal, string reason)
    {
        return this.Engine.RejectSession(proposal, reason);
    }

    public Task UpdateSession(string topic, Namespaces namespaces)
    {
        return this.Engine.UpdateSession(topic, namespaces);
    }

    public Task ExtendSession(string topic)
    {
        return this.Engine.ExtendSession(topic);
    }

    public Task RespondSessionRequest<T, TR>(string topic, JsonRpcResponse<TR> response)
    {
        return this.Engine.RespondSessionRequest<T, TR>(topic, response);
    }

    public Task EmitSessionEvent<T>(string topic, EventData<T> eventData, string chainId)
    {
        return this.Engine.EmitSessionEvent(topic, eventData, chainId);
    }

    public Task DisconnectSession(string topic, Error reason)
    {
        return this.Engine.DisconnectSession(topic, reason);
    }

    public Task RespondAuthRequest(ResultResponse results, string iss)
    {
        return this.Engine.RespondAuthRequest(results, iss);
    }

    public Task RespondAuthRequest(AuthErrorResponse error, string iss)
    {
        return this.Engine.RespondAuthRequest(error, iss);
    }

    public Task RespondAuthRequest(AuthRequest request, Error error, string iss)
    {
        return this.Engine.RespondAuthRequest(request, error, iss);
    }

    public Task RespondAuthRequest(AuthRequest request, string signature, string iss, bool eip191 = true)
    {
        return this.Engine.RespondAuthRequest(request, signature, iss, eip191);
    }

    public string FormatMessage(Cacao.CacaoRequestPayload payload, string iss)
    {
        return this.Engine.FormatMessage(payload, iss);
    }

    private Task Initialize()
    {
        return this.Engine.Init();
    }

    public event EventHandler<AuthRequest> AuthRequested
    {
        add
        {
            Engine.AuthRequested += value;
        }
        remove
        {
            Engine.AuthRequested -= value;
        }
    }
    public event EventHandler<AuthResponse> AuthResponded
    {
        add
        {
            Engine.AuthResponded += value;
        }
        remove
        {
            Engine.AuthResponded -= value;
        }
    }
    public event EventHandler<AuthErrorResponse> AuthError
    {
        add
        {
            Engine.AuthError += value;
        }
        remove
        {
            Engine.AuthError -= value;
        }
    }

    public void Dispose()
    {
        Core?.Dispose();
    }
}
