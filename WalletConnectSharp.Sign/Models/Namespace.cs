using Newtonsoft.Json;

namespace WalletConnectSharp.Sign.Models
{
    /// <summary>
    /// A namespace that holds accounts, methods and events enabled. Also includes
    /// extension namespaces that are enabled
    /// </summary>
    public sealed class Namespace
    {
        public Namespace(ProposedNamespace proposedNamespace)
        {
            this.Methods = proposedNamespace.Methods;
            this.Chains = proposedNamespace.Chains;
            this.Events = proposedNamespace.Events;
        }

        public Namespace() { }

        /// <summary>
        /// An array of all accounts enabled in this namespace
        /// </summary>
        [JsonProperty("accounts")]
        public string[] Accounts;

        /// <summary>
        /// An array of all methods enabled in this namespace
        /// </summary>
        [JsonProperty("methods")]
        public string[] Methods;

        /// <summary>
        /// An array of all events enabled in this namespace
        /// </summary>
        [JsonProperty("events")]
        public string[] Events;

        /// <summary>
        /// An array of all chains enabled in this namespace
        /// </summary>
        [JsonProperty("chains")] public string[] Chains;

        public Namespace WithMethod(string method)
        {
            Methods = Methods == null
                ? [method]
                : Methods.Append(method).ToArray();
            return this;
        }

        public Namespace WithChain(string chain)
        {
            Chains = Chains == null
                ? [chain]
                : Chains.Append(chain).ToArray();
            return this;
        }

        public Namespace WithEvent(string @event)
        {
            Events = Events == null
                ? [@event]
                : Events.Append(@event).ToArray();
            return this;
        }

        public Namespace WithAccount(string account)
        {
            Accounts = Accounts == null
                ? [account]
                : Accounts.Append(account).ToArray();
            return this;
        }

        private static bool ArrayEquals(string[] a, string[] b)
        {
            return a.Length == b.Length && Array.TrueForAll(a, b.Contains) && Array.TrueForAll(b, a.Contains);
        }

        private bool Equals(Namespace other)
        {
            return ArrayEquals(Accounts, other.Accounts) && ArrayEquals(Methods, other.Methods) &&
                   ArrayEquals(Events, other.Events);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != this.GetType())
            {
                return false;
            }

            return Equals((Namespace)obj);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Accounts, Methods, Events);
        }

        public bool TryGetChains(out string[] chainIds)
        {
            if (Chains is { Length: 0 })
            {
                chainIds = Chains;
                return true;
            }

            HashSet<string> chainSet = [];
            foreach (var account in Accounts)
            {
                var t = false;
                for (var i = 0; i < account.Length; i++)
                {
                    if (account[i] != ':')
                    {
                        continue;
                    }

                    if (!t)
                    {
                        t = true;
                    }
                    else
                    {
                        chainSet.Add(account[..i]);
                        break;
                    }
                }
            }

            chainIds = chainSet.ToArray();
            return true;
        }

        private sealed class NamespaceEqualityComparer : IEqualityComparer<Namespace>
        {
            public bool Equals(Namespace x, Namespace y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (ReferenceEquals(x, null))
                {
                    return false;
                }

                if (ReferenceEquals(y, null))
                {
                    return false;
                }

                if (x.GetType() != y.GetType())
                {
                    return false;
                }

                return x.Accounts.SequenceEqual(y.Accounts) && x.Methods.SequenceEqual(y.Methods) &&
                       x.Events.SequenceEqual(y.Events);
            }

            public int GetHashCode(Namespace obj)
            {
                return HashCode.Combine(obj.Accounts, obj.Methods, obj.Events);
            }
        }

        public static IEqualityComparer<Namespace> NamespaceComparer { get; } = new NamespaceEqualityComparer();
    }
}
