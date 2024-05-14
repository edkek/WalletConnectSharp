using Newtonsoft.Json;
using WalletConnectSharp.Core;
using WalletConnectSharp.Core.Interfaces;
using WalletConnectSharp.Core.Models.Relay;

namespace WalletConnectSharp.Sign.Models
{
    /// <summary>
    /// A struct that holds session data, including the session topic, when the session expires, whether the session has
    /// been acknowledged and who the session controller is.
    /// </summary>
    public struct SessionStruct : IKeyHolder<string>
    {
        /// <summary>
        /// The topic of this session
        /// </summary>
        [JsonProperty("topic")]
        public string Topic;
        
        /// <summary>
        /// The pairing topic of this session
        /// </summary>
        [JsonProperty("pairingTopic")]
        public string PairingTopic;
        
        /// <summary>
        /// The relay protocol options this session is using
        /// </summary>
        [JsonProperty("relay")]
        public ProtocolOptions Relay;
        
        /// <summary>
        /// When this session expires
        /// </summary>
        [JsonProperty("expiry")]
        public long? Expiry;
        
        /// <summary>
        /// Whether this session has been acknowledged or not
        /// </summary>
        [JsonProperty("acknowledged")]
        public bool? Acknowledged;
        
        /// <summary>
        /// The public key of the current controller for this session
        /// </summary>
        [JsonProperty("controller")]
        public string Controller;
        
        /// <summary>
        /// The enabled namespaces this session uses
        /// </summary>
        [JsonProperty("namespaces")]
        public Namespaces Namespaces;
        
        /// <summary>
        /// The required enabled namespaces this session uses
        /// </summary>
        [JsonProperty("requiredNamespaces")]
        public RequiredNamespaces RequiredNamespaces;
   
        /// <summary>
        /// The <see cref="Participant"/> data that represents ourselves in this session
        /// </summary>
        [JsonProperty("self")]
        public Participant Self;
        
        /// <summary>
        /// The <see cref="Participant"/> data that represents the peer in this session
        /// </summary>
        [JsonProperty("peer")]
        public Participant Peer;

        /// <summary>
        /// This is the key field, mapped to the Topic. Implemented for <see cref="IKeyHolder{TKey}"/>
        /// so this struct can be stored using <see cref="IStore{TKey,TValue}"/>
        /// </summary>
        [JsonIgnore]
        public string Key
        {
            get
            {
                return Topic;
            }
        }

        public Caip25Address CurrentAddress(string chainId)
        {
            ValidateChainIdAndTopic(chainId);

            var namespaceStr = chainId.Split(':')[0];

            if (!Namespaces.TryGetValue(namespaceStr, out var defaultNamespace))
            {
                throw new InvalidOperationException(
                    $"SessionStruct.CurrentAddress: Given namespace {namespaceStr} is not available in the current session");
            }
            
            if (defaultNamespace.Accounts.Length == 0)
                throw new InvalidOperationException(
                    $"SessionStruct.CurrentAddress: Given namespace {namespaceStr} has no connected addresses");

            var fullAddress = Array.Find(defaultNamespace.Accounts, addr => addr.StartsWith(chainId));
            if (fullAddress == default)
            {
                throw new InvalidOperationException(
                    $"SessionStruct.CurrentAddress: No address found for chain {chainId}");
            }

            var address = fullAddress.Split(":")[2];
            return new Caip25Address { Address = address, ChainId = chainId };
        }

        public IEnumerable<Caip25Address> AllAddresses(string @namespace)
        {
            ValidateNamespaceAndTopic(@namespace);
        
            var defaultNamespace = Namespaces[@namespace];
            return defaultNamespace.Accounts.Length == 0
                ? []
                : defaultNamespace.Accounts.Select(CreateCaip25Address);
        }

        public static Caip25Address CreateCaip25Address(string fullAddress)
        {
            var addressParts = fullAddress.Split(":");
            var address = addressParts[2];
            var chainId = string.Join(':', addressParts.Take(2));

            return new Caip25Address { Address = address, ChainId = chainId };
        }

        private void ValidateNamespaceAndTopic(string @namespace)
        {
            if (@namespace == null)
            {
                throw new ArgumentException("@namespace is null");
            }

            if (string.IsNullOrWhiteSpace(Topic))
            {
                throw new ArgumentException("Session is undefined");
            }
        }

        private void ValidateChainIdAndTopic(string chainId)
        {
            if (string.IsNullOrWhiteSpace(chainId))
            {
                throw new ArgumentException("chainId is null or empty");
            }

            if (!Utils.IsValidChainId(chainId))
            {
                throw new ArgumentException("The format of 'chainId' is invalid. Must be in the format of 'namespace:chainId' (e.g. 'eip155:10'). See CAIP-2 for more information.");
            }

            if (string.IsNullOrWhiteSpace(Topic))
            {
                throw new ArgumentException("Session is undefined");
            }
        }
    }
}
