﻿using Newtonsoft.Json;
using WalletConnectSharp.Common.Logging;
using WalletConnectSharp.Sign.Interfaces;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Sign.Models.Engine.Events;

namespace WalletConnectSharp.Sign.Controllers;

public class AddressProvider : IAddressProvider
{
    public struct DefaultData
    {
        public SessionStruct Session;
        public string Namespace;
        public string ChainId;
    }

    public event EventHandler<DefaultsLoadingEventArgs> DefaultsLoading;

    public bool HasDefaultSession
    {
        get
        {
            return !string.IsNullOrWhiteSpace(DefaultSession.Topic) && DefaultSession.RequiredNamespaces != null;
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

    public string DefaultChain
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
        client.SessionUpdated += ClientOnSessionUpdated;
        client.SessionApproved += ClientOnSessionConnected;
    }

    public virtual async Task SaveDefaults()
    {
        await _client.Core.Storage.SetItem($"{Context}-default-session", _state);
    }

    public virtual async Task LoadDefaults()
    {
        var key = $"{Context}-default-session";
        if (await _client.Core.Storage.HasItem(key))
        {
            _state = await _client.Core.Storage.GetItem<DefaultData>($"{Context}-default-session");
        }
        else
        {
            _state = new DefaultData();
        }
        
        DefaultsLoading?.Invoke(this, new DefaultsLoadingEventArgs(ref _state));
    }
    
    private void ClientOnSessionUpdated(object sender, SessionEvent e)
    {
        if (DefaultSession.Topic == e.Topic)
        {
            UpdateDefaultChainAndNamespace();
        }
    }

    private void ClientOnSessionDeleted(object sender, SessionEvent e)
    {
        if (DefaultSession.Topic == e.Topic)
        {
            DefaultSession = default;
            UpdateDefaultChainAndNamespace();
        }
    }

    private void ClientOnSessionConnected(object sender, SessionStruct e)
    {
        if (!HasDefaultSession)
        {
            DefaultSession = e;
            UpdateDefaultChainAndNamespace();
        }
    }

    private async void UpdateDefaultChainAndNamespace()
    {
        try
        {
            if (HasDefaultSession)
            {
                var currentDefault = DefaultNamespace;
                if (currentDefault != null && DefaultSession.RequiredNamespaces.ContainsKey(currentDefault))
                {
                    // DefaultNamespace is still valid
                    var currentChain = DefaultChain;
                    if (currentChain == null ||
                        DefaultSession.RequiredNamespaces[DefaultNamespace].Chains.Contains(currentChain))
                    {
                        // DefaultChain is still valid
                        return;
                    }

                    DefaultChain = DefaultSession.RequiredNamespaces[DefaultNamespace].Chains[0];
                    return;
                }

                // DefaultNamespace is null or not found in RequiredNamespaces, update it
                DefaultNamespace = DefaultSession.RequiredNamespaces.OrderedKeys.FirstOrDefault();
                if (DefaultNamespace != null)
                {
                    DefaultChain = DefaultSession.RequiredNamespaces[DefaultNamespace].Chains[0];
                }
                else
                {
                    // TODO The Keys property is unordered! Maybe this needs to be updated
                    DefaultNamespace = DefaultSession.RequiredNamespaces.Keys.FirstOrDefault();
                    if (DefaultNamespace != null)
                    {
                        DefaultChain = DefaultSession.RequiredNamespaces[DefaultNamespace].Chains[0];
                    }
                }
            }
            else
            {
                DefaultNamespace = null;
            }
        }
        finally
        {
            await SaveDefaults();
        }
    }

    public Caip25Address CurrentAddress(string @namespace = null, SessionStruct session = default)
    {
        @namespace ??= DefaultNamespace;
        if (string.IsNullOrWhiteSpace(session.Topic)) // default
            session = DefaultSession;

        return session.CurrentAddress(@namespace);
    }

    public async Task Init()
    {
        await this.LoadDefaults();
    }

    public Caip25Address[] AllAddresses(string @namespace = null, SessionStruct session = default)
    {
        @namespace ??= DefaultNamespace;
        if (string.IsNullOrWhiteSpace(session.Topic)) // default
            session = DefaultSession;

        return session.AllAddresses(@namespace);
    }

    public void Dispose()
    {
        _client.SessionConnected -= ClientOnSessionConnected;
        _client.SessionDeleted -= ClientOnSessionDeleted;
        _client.SessionUpdated -= ClientOnSessionUpdated;
        _client.SessionApproved -= ClientOnSessionConnected; 

        _client = null;
        Sessions = null;
        DefaultNamespace = null;
        DefaultSession = default;
    }
}