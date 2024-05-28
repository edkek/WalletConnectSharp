using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Sign.Interfaces;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Sign.Models.Engine.Events;

namespace WalletConnectSharp.Sign.Controllers;

public class AddressProvider : IAddressProvider
{
    private bool _disposed;
    
    public struct DefaultData
    {
        public SessionStruct Session;
        public string Namespace;
        public string ChainId;
    }

    public event EventHandler<DefaultsLoadingEventArgs> DefaultsLoaded;

    public bool HasDefaultSession
    {
        get
        {
            return !string.IsNullOrWhiteSpace(DefaultSession.Topic) && DefaultSession.Namespaces != null;
        }
    }

    public string Name
    {
        get
        {
            return $"{_client.Name}-address-provider";
        }
    }

    public string Context
    {
        get
        {
            return Name;
        }
    }

    private DefaultData _state;

    public SessionStruct DefaultSession
    {
        get
        {
            return _state.Session;
        }
        set
        {
            _state.Session = value;
        }
    }

    public string DefaultNamespace
    {
        get
        {
            return _state.Namespace;
        }
        set
        {
            _state.Namespace = value;
        }
    }

    public string DefaultChainId
    {
        get
        {
            return _state.ChainId;
        }
        set
        {
            _state.ChainId = value;
        }
    }

    public ISession Sessions { get; private set; }

    private ISignClient _client;

    public AddressProvider(ISignClient client)
    {
        this._client = client;
        this.Sessions = client.Session;

        // set the first connected session to the default one
        client.SessionConnected += ClientOnSessionConnected;
        client.SessionDeleted += ClientOnSessionDeleted;
        client.SessionUpdateRequest += ClientOnSessionUpdated;
        client.SessionApproved += ClientOnSessionConnected;
    }

    public virtual async Task SaveDefaults()
    {
        await _client.Core.Storage.SetItem($"{Context}-default-session", _state);
    }

    public virtual async Task LoadDefaultsAsync()
    {
        var key = $"{Context}-default-session";
        if (await _client.Core.Storage.HasItem(key))
        {
            var state = await _client.Core.Storage.GetItem<DefaultData>(key);
            var sessionExpiry = state.Session.Expiry;

            _state = sessionExpiry != null && !Clock.IsExpired(sessionExpiry.Value)
                ? state
                : new DefaultData();
        }
        else
        {
            _state = new DefaultData();
        }

        DefaultsLoaded?.Invoke(this, new DefaultsLoadingEventArgs(_state));
    }

    private async void ClientOnSessionUpdated(object sender, SessionEvent e)
    {
        if (DefaultSession.Topic == e.Topic)
        {
            DefaultSession = Sessions.Get(e.Topic);
            await UpdateDefaultChainIdAndNamespaceAsync();
        }
    }

    private async void ClientOnSessionDeleted(object sender, SessionEvent e)
    {
        if (DefaultSession.Topic == e.Topic)
        {
            DefaultSession = default;
            await UpdateDefaultChainIdAndNamespaceAsync();
        }
    }

    private async void ClientOnSessionConnected(object sender, SessionStruct e)
    {
        DefaultSession = e;
        await UpdateDefaultChainIdAndNamespaceAsync();
    }

    private async Task UpdateDefaultChainIdAndNamespaceAsync()
    {
        if (HasDefaultSession)
        {
            // Check if current default namespace is still valid with the current session
            var currentDefault = DefaultNamespace;

            
            if (currentDefault != null && DefaultSession.Namespaces.ContainsKey(currentDefault))
            {
                if (!DefaultSession.Namespaces[DefaultNamespace].TryGetChains(out var approvedChains))
                {
                    throw new InvalidOperationException("Could not get chains for current default namespace");
                }
                
                // Check if current default chain is still valid with the current session
                var currentChain = DefaultChainId;

                if (currentChain == null || !approvedChains.Contains(currentChain))
                {
                    // If the current default chain is not valid, let's use the first one
                    DefaultChainId = approvedChains[0];
                }
            }
            else
            {
                // If DefaultNamespace is null or not found in current available spaces, update it
                DefaultNamespace = DefaultSession.Namespaces.Keys.FirstOrDefault();
                if (DefaultNamespace != null)
                {
                    if (!DefaultSession.Namespaces[DefaultNamespace].TryGetChains(out var approvedChains))
                    {
                        throw new InvalidOperationException("Could not get chains for current default namespace");
                    }

                    DefaultChainId = approvedChains[0];
                }
                else
                {
                    throw new InvalidOperationException("Could not figure out default chain and namespace");
                }
            }

            await SaveDefaults();
        }
        else
        {
            DefaultNamespace = null;
            DefaultChainId = null;
        }
    }

    public async Task SetDefaultNamespaceAsync(string @namespace)
    {
        if (string.IsNullOrWhiteSpace(@namespace))
        {
            throw new ArgumentNullException(nameof(@namespace));
        }

        if (!DefaultSession.Namespaces.ContainsKey(@namespace))
        {
            throw new InvalidOperationException($"Namespace {@namespace} is not available in the current session");
        }

        DefaultNamespace = @namespace;
        await SaveDefaults();
    }
    
    public async Task SetDefaultChainIdAsync(string chainId)
    {
        if (string.IsNullOrWhiteSpace(chainId))
        {
            throw new ArgumentNullException(nameof(chainId));
        }

        DefaultChainId = chainId;
        await SaveDefaults();
    }

    public Caip25Address CurrentAddress(string chainId = null, SessionStruct session = default)
    {
        chainId ??= DefaultChainId;
        if (string.IsNullOrWhiteSpace(session.Topic))
        {
            session = DefaultSession;
        }

        return session.CurrentAddress(chainId);
    }

    public IEnumerable<Caip25Address> AllAddresses(string @namespace = null, SessionStruct session = default)
    {
        @namespace ??= DefaultNamespace;
        if (string.IsNullOrWhiteSpace(session.Topic)) // default
            session = DefaultSession;

        return session.AllAddresses(@namespace);
    }
    
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _client.SessionConnected -= ClientOnSessionConnected;
            _client.SessionDeleted -= ClientOnSessionDeleted;
            _client.SessionUpdateRequest -= ClientOnSessionUpdated;
            _client.SessionApproved -= ClientOnSessionConnected;

            _client = null;
            Sessions = null;
            DefaultNamespace = null;
            DefaultSession = default;
        }

        _disposed = true;
    }
}
