﻿using WalletConnectSharp.Sign.Interfaces;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Sign.Models.Engine.Events;

namespace WalletConnectSharp.Sign.Controllers;

public class AddressProvider : IAddressProvider
{
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

    public SessionStruct DefaultSession { get; set; }
    public string DefaultNamespace { get; set; }
    public string DefaultChain { get; set; }
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

    private void UpdateDefaultChainAndNamespace()
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

    public Caip25Address CurrentAddress(string @namespace = null, SessionStruct session = default)
    {
        @namespace ??= DefaultNamespace;
        if (string.IsNullOrWhiteSpace(session.Topic)) // default
            session = DefaultSession;

        // double check
        if (@namespace == null)
            throw new ArgumentException("CurrentAddress: @namespace is null");
        if (string.IsNullOrWhiteSpace(session.Topic))
            throw new ArgumentException("CurrentAddress: Session is undefined");
        
        var defaultNamespace = session.Namespaces[@namespace];

        if (defaultNamespace.Accounts.Length == 0)
            return null; //The namespace {@namespace} has no addresses connected")

        var fullAddress = defaultNamespace.Accounts[0];
        var addressParts = fullAddress.Split(":");

        var address = addressParts[2];
        var chainId = string.Join(':', addressParts.Take(2));

        return new Caip25Address()
        {
            Address = address,
            ChainId = chainId,
        };
    }

    public Caip25Address[] AllAddresses(string @namespace = null, SessionStruct session = default)
    {
        @namespace ??= DefaultNamespace;
        if (string.IsNullOrWhiteSpace(session.Topic)) // default
            session = DefaultSession;

        // double check
        if (@namespace == null)
            throw new ArgumentException("CurrentAddress: @namespace is null");
        if (string.IsNullOrWhiteSpace(session.Topic))
            throw new ArgumentException("CurrentAddress: Session is undefined");
        
        var defaultNamespace = session.Namespaces[@namespace];

        if (defaultNamespace.Accounts.Length == 0)
            return null; //The namespace {@namespace} has no addresses connected")

        return defaultNamespace.Accounts.Select(addr => new Caip25Address()
        {
            Address = addr.Split(":")[2], ChainId = string.Join(":", addr.Split(":").Take(2))
        }).ToArray();
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