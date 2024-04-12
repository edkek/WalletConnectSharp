using WalletConnectSharp.Common.Model.Relay;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Core.Interfaces;
using WalletConnectSharp.Core.Models.Publisher;
using WalletConnectSharp.Core.Models.Relay;
using WalletConnectSharp.Network.Models;

namespace WalletConnectSharp.Core.Controllers
{
    /// <summary>
    /// The publisher module is responsible for publishing messages asynchronously. 
    /// </summary>
    public class Publisher : IPublisher
    {
        public event EventHandler<PublishParams> OnPublishedMessage;

        /// <summary>
        /// The Relayer this publisher module uses to publish messages
        /// </summary>
        public IRelayer Relayer { get; }

        /// <summary>
        /// The name of this publisher module
        /// </summary>
        public string Name
        {
            get
            {
                return $"{Relayer.Name}-publisher";
            }
        }

        /// <summary>
        /// The context string this publisher module is using
        /// </summary>
        public string Context
        {
            get
            {
                return Name;
            }
        }

        /// <summary>
        /// Create a new Publisher that uses the given IRelayer to publish messages to
        /// </summary>
        /// <param name="relayer">The IRelayer to publish messages to</param>
        public Publisher(IRelayer relayer)
        {
            Relayer = relayer;
        }

        protected Task RpcPublish(string topic, string message, long ttl, long tag, ProtocolOptions relay,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var api = RelayProtocols.GetRelayProtocol(relay.Protocol);
            var request = new RequestArguments<RelayPublishRequest>()
            {
                Method = api.Publish,
                Params = new RelayPublishRequest()
                {
                    Message = message,
                    Topic = topic,
                    TTL = ttl,
                    Tag = tag
                }
            };

            return Relayer.Request<RelayPublishRequest, object>(request, this);
        }
        
        /// <summary>
        /// Publish a messages to the given topic, optionally specifying publish options.
        /// </summary>
        /// <param name="topic">The topic to send the message in</param>
        /// <param name="message">The message to send</param>
        /// <param name="opts">(Optional) publish options specifying TTL and tag</param>
        public async Task Publish(string topic, string message, PublishOptions opts = null)
        {
            if (opts == null)
            {
                opts = new PublishOptions
                {
                    Relay = new ProtocolOptions
                    {
                        Protocol = RelayProtocols.Default
                    },
                    Tag = 0,
                    TTL = Clock.SIX_HOURS,
                };
            }
            else
            {
                opts.Relay ??= new ProtocolOptions { Protocol = RelayProtocols.Default };
            }

            var @params = new PublishParams()
            {
                Message = message,
                Options = opts,
                Topic = topic
            };

            await Publish(@params);
        }

        /// <summary>
        /// Publish a messages to the given topic, optionally specifying publish options.
        /// </summary>
        /// <param name="params">Parameters to publish</param>
        public async Task Publish(PublishParams @params)
        {
            await RpcPublish(
                @params.Topic,
                @params.Message,
                @params.Options.TTL,
                @params.Options.Tag,
                @params.Options.Relay);

            OnPublishedMessage?.Invoke(this, @params);
        }

        public void Dispose()
        {
        }
    }
}
