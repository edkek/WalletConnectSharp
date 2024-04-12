using Newtonsoft.Json;
using WalletConnectSharp.Core.Interfaces;
using WalletConnectSharp.Network;

namespace WalletConnectSharp.Core.Models.Relay
{
    /// <summary>
    /// The options for configuring the Relayer module
    /// </summary>
    public class RelayerOptions
    {
        /// <summary>
        /// The ICore instance the Relayer should use. An ICore module is required as the Relayer
        /// module requires the core modules to function properly
        /// </summary>
        [JsonProperty("core")] public ICore Core;

        /// <summary>
        /// The URL of the Relay server to connect to. This should not include any auth information, the Relayer module
        /// will construct it's own auth token using the project ID specified
        /// </summary>
        [JsonProperty("relayUrl")] public string RelayUrl;

        /// <summary>
        /// The project ID to use for Relay authentication
        /// </summary>
        [JsonProperty("projectId")]
        public string ProjectId { get; set; }

        /// <summary>
        /// How long the <see cref="IRelayer"/> should wait before throwing a <see cref="TimeoutException"/> during
        /// the connection phase. If this field is null, then the timeout will be infinite.
        /// </summary>
        public TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// The interval at which the Relayer should request new (unsent) messages from the Relay server. If
        /// this field is null, then the Relayer will never ask for new messages, so all new messages will only
        /// come directly from Relay server 
        /// </summary>
        public TimeSpan MessageFetchInterval { get; set; } = TimeSpan.FromSeconds(15);


        /// <summary>
        /// The <see cref="IRelayUrlBuilder"/> module to use for building the Relay RPC URL.
        /// </summary>
        public IRelayUrlBuilder RelayUrlBuilder { get; set; }
    }
}
