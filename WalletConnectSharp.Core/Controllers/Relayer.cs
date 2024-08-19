using Newtonsoft.Json;
using WalletConnectSharp.Common.Logging;
using WalletConnectSharp.Common.Model.Errors;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Core.Interfaces;
using WalletConnectSharp.Core.Models;
using WalletConnectSharp.Core.Models.Relay;
using WalletConnectSharp.Core.Models.Subscriber;
using WalletConnectSharp.Network;
using WalletConnectSharp.Network.Models;

namespace WalletConnectSharp.Core.Controllers
{
    /// <summary>
    /// The Relayer module handles the interaction with the WalletConnect relayer server.
    /// Each Relayer module uses a Publisher, Subscriber and a JsonRPCProvider.
    /// </summary>
    public class Relayer : IRelayer
    {
        private bool _transportExplicitlyClosed = false;

        /// <summary>
        /// The default relay server URL used when no relay URL is given
        /// </summary>
        public static readonly string DEFAULT_RELAY_URL = "wss://relay.walletconnect.org";

        /// <summary>
        /// The Name of this Relayer module
        /// </summary>
        public string Name
        {
            get
            {
                return $"{Core.Name}-relayer";
            }
        }

        /// <summary>
        /// The context string this Relayer module is using
        /// </summary>
        public string Context
        {
            get
            {
                return Name;
            }
        }

        public event EventHandler OnConnected;
        public event EventHandler OnDisconnected;
        public event EventHandler<Exception> OnErrored;
        public event EventHandler<MessageEvent> OnMessageReceived;
        public event EventHandler OnTransportClosed;
        public event EventHandler OnConnectionStalled;

        /// <summary>
        /// The ICore module that is using this Relayer module
        /// </summary>
        public ICore Core { get; }

        /// <summary>
        /// The ISubscriber module that this Relayer module is using
        /// </summary>
        public ISubscriber Subscriber { get; }

        /// <summary>
        /// The IPublisher module that this Relayer module is using
        /// </summary>
        public IPublisher Publisher { get; }

        /// <summary>
        /// The IMessageTracker module that this Relayer module is using
        /// </summary>
        public IMessageTracker Messages { get; }

        /// <summary>
        /// The IJsonRpcProvider module that this Relayer module is using
        /// </summary>
        public IJsonRpcProvider Provider { get; private set; }

        /// <summary>
        /// The IRelayUrlBuilder module that this Relayer module is using during Provider creation
        /// </summary>
        public IRelayUrlBuilder RelayUrlBuilder { get; private set; }

        /// <summary>
        /// How long the <see cref="IRelayer"/> should wait before throwing a <see cref="TimeoutException"/> during
        /// the connection phase. If this field is null, then the timeout will be infinite.
        /// </summary>
        public TimeSpan? ConnectionTimeout { get; set; }

        /// <summary>
        /// Whether this Relayer is connected
        /// </summary>
        public bool Connected
        {
            get
            {
                return this.Provider.Connection.Connected;
            }
        }

        /// <summary>
        /// Whether this Relayer is currently connecting
        /// </summary>
        public bool Connecting
        {
            get
            {
                return this.Provider.Connection.Connecting;
            }
        }

        public bool TransportExplicitlyClosed
        {
            get
            {
                return _transportExplicitlyClosed;
            }
        }

        private readonly string projectId;
        private string relayUrl;
        private bool initialized;
        private bool reconnecting = false;
        protected bool Disposed;
        private DateTime lastSyncTime;
        private bool isSyncing;

        /// <summary>
        /// Create a new Relayer with the given RelayerOptions.
        /// </summary>
        /// <param name="opts">The options that must be specified. This includes the ICore module
        /// using this module, the RelayURL (optional) and the project Id</param>
        public Relayer(RelayerOptions opts)
        {
            Core = opts.Core;
            Messages = new MessageTracker(Core);
            Subscriber = new Subscriber(this);
            Publisher = new Publisher(this);

            relayUrl = opts.RelayUrl;
            if (string.IsNullOrWhiteSpace(relayUrl))
            {
                relayUrl = DEFAULT_RELAY_URL;
            }

            projectId = opts.ProjectId;

            ConnectionTimeout = opts.ConnectionTimeout;
            RelayUrlBuilder = opts.RelayUrlBuilder;
            MessageFetchInterval = opts.MessageFetchInterval;
        }

        public TimeSpan? MessageFetchInterval { get; set; }

        /// <summary>
        /// Initialize this Relayer module. This will initialize all sub-modules
        /// and connect the backing IJsonRpcProvider.
        /// </summary>
        public async Task Init()
        {
            WCLogger.Log("[Relayer] Creating provider");
            await CreateProvider();

            WCLogger.Log("[Relayer] Opening transport");
            await TransportOpen();

            WCLogger.Log("[Relayer] Init MessageHandler and Subscriber");
            await Task.WhenAll(
                Messages.Init(), Subscriber.Init()
            );

            WCLogger.Log("[Relayer] Registering event listeners");
            RegisterEventListeners();

            initialized = true;
        }

        protected virtual async Task CreateProvider()
        {
            var auth = await this.Core.Crypto.SignJwt(this.relayUrl);
            Provider = await CreateProvider(auth);
            RegisterProviderEventListeners();
        }

        protected virtual async Task<IJsonRpcProvider> CreateProvider(string auth)
        {
            var connection = await BuildConnection(
                RelayUrlBuilder.FormatRelayRpcUrl(
                    relayUrl,
                    IRelayer.Protocol,
                    IRelayer.Version.ToString(),
                    projectId,
                    auth)
            );

            return new JsonRpcProvider(connection);
        }

        protected virtual Task<IJsonRpcConnection> BuildConnection(string url)
        {
            return Core.Options.ConnectionBuilder.CreateConnection(url);
        }

        protected virtual void RegisterProviderEventListeners()
        {
            Provider.RawMessageReceived += OnProviderRawMessageReceived;
            Provider.Connected += OnProviderConnected;
            Provider.Disconnected += OnProviderDisconnected;
            Provider.ErrorReceived += OnProviderErrorReceived;
        }

        private void OnProviderErrorReceived(object sender, Exception e)
        {
            if (Disposed) return;

            this.OnErrored?.Invoke(this, e);
        }

        private async void OnProviderDisconnected(object sender, EventArgs e)
        {
            if (Disposed) return;

            this.OnDisconnected?.Invoke(this, EventArgs.Empty);

            if (this._transportExplicitlyClosed)
                return;

            await RestartTransport();
        }

        private void OnProviderConnected(object sender, IJsonRpcConnection e)
        {
            if (Disposed) return;
            
            this.OnConnected?.Invoke(sender, EventArgs.Empty);
        }

        private void OnProviderRawMessageReceived(object sender, string e)
        {
            if (Disposed) return;

            OnProviderPayload(e);
        }

        protected virtual void RegisterEventListeners()
        {
            this.OnConnectionStalled += OnConnectionStalledHandler;
        }

        private async void OnConnectionStalledHandler(object sender, EventArgs e)
        {
            if (this.Provider.Connection.IsPaused)
                return;

            await this.RestartTransport();
        }

        protected virtual async void OnProviderPayload(string payloadJson)
        {
            var payload = JsonConvert.DeserializeObject<JsonRpcPayload>(payloadJson);

            if (payload != null && payload.IsRequest && payload.Method.EndsWith("_subscription"))
            {
                var @event = JsonConvert.DeserializeObject<JsonRpcRequest<JsonRpcSubscriptionParams>>(payloadJson);

                var messageEvent = new MessageEvent()
                {
                    Message = @event.Params.Data.Message, Topic = @event.Params.Data.Topic
                };

                await AcknowledgePayload(payload);
                await OnMessageEvent(messageEvent);
            }
        }

        protected virtual async Task<bool> ShouldIgnoreMessageEvent(MessageEvent messageEvent)
        {
            var isSubscribed = await Subscriber.IsSubscribed(messageEvent.Topic);
            if (!isSubscribed)
            {
                return true;
            }

            var exists = Messages.Has(messageEvent.Topic, messageEvent.Message);
            return exists;
        }

        protected virtual Task RecordMessageEvent(MessageEvent messageEvent)
        {
            return Messages.Set(messageEvent.Topic, messageEvent.Message);
        }

        protected virtual async Task OnMessageEvent(MessageEvent messageEvent)
        {
            if (await ShouldIgnoreMessageEvent(messageEvent)) return;

            this.OnMessageReceived?.Invoke(this, messageEvent);
            await RecordMessageEvent(messageEvent);
        }

        protected virtual async Task AcknowledgePayload(JsonRpcPayload payload)
        {
            var response = new JsonRpcResponse<bool>() { Id = payload.Id, Result = true };
            await Provider.Connection.SendResult(response, this);
        }

        /// <summary>
        /// Publish a message to this Relayer in the given topic (optionally) specifying
        /// PublishOptions.
        /// </summary>
        /// <param name="topic">The topic to publish the message in</param>
        /// <param name="message">The message to publish</param>
        /// <param name="opts">(Optional) Publish options to specify TTL and tag</param>
        public async Task Publish(string topic, string message, PublishOptions opts = null)
        {
            IsInitialized();
            await Publisher.Publish(topic, message, opts);
            await RecordMessageEvent(new MessageEvent() { Topic = topic, Message = message });
        }

        /// <summary>
        /// Subscribe to a given topic optionally specifying Subscribe options
        /// </summary>
        /// <param name="topic">The topic to subscribe to</param>
        /// <param name="opts">(Optional) Subscribe options that specify protocol options</param>
        /// <returns></returns>
        public async Task<string> Subscribe(string topic, SubscribeOptions opts = null)
        {
            IsInitialized();
            var ids = this.Subscriber.TopicMap.Get(topic);
            if (ids.Length > 0)
            {
                return ids[0];
            }

            TaskCompletionSource<string> task1 = new TaskCompletionSource<string>();

            EventUtils.ListenOnce<ActiveSubscription>(
                (sender, subscription) =>
                {
                    if (subscription.Topic == topic)
                        task1.TrySetResult("");
                },
                h => this.Subscriber.Created += h,
                h => this.Subscriber.Created -= h
            );

            return (await Task.WhenAll(
                task1.Task,
                this.Subscriber.Subscribe(topic, opts)
            ))[1];
        }

        /// <summary>
        /// Unsubscribe to a given topic optionally specify unsubscribe options
        /// </summary>
        /// <param name="topic">Tbe topic to unsubscribe to</param>
        /// <param name="opts">(Optional) Unsubscribe options specifying protocol options</param>
        /// <returns></returns>
        public Task Unsubscribe(string topic, UnsubscribeOptions opts = null)
        {
            IsInitialized();
            return Subscriber.Unsubscribe(topic, opts);
        }

        /// <summary>
        /// Send a Json RPC request with a parameter field of type T, and decode a response with the type of TR.
        /// </summary>
        /// <param name="request">The json rpc request to send</param>
        /// <param name="context">The current context</param>
        /// <typeparam name="T">The type of the parameter field in the json rpc request</typeparam>
        /// <typeparam name="TR">The type of the parameter field in the json rpc response</typeparam>
        /// <returns>The decoded response for the request</returns>
        public async Task<TR> Request<T, TR>(IRequestArguments<T> request, object context = null)
        {
            WCLogger.Log("[Relayer] Checking for established connection");
            await ToEstablishConnection();

            WCLogger.Log("[Relayer] Sending request through provider");
            var result = await Provider.Request<T, TR>(request, context);

            return result;
        }

        public async Task TransportClose()
        {
            _transportExplicitlyClosed = true;
            if (Connected)
            {
                await this.Provider.Disconnect();
                this.OnTransportClosed?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task TransportOpen(string relayUrl = null)
        {
            this._transportExplicitlyClosed = false;
            if (reconnecting) return;
            this.relayUrl = relayUrl ?? this.relayUrl;
            this.reconnecting = true;
            try
            {
                TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();
                if (!this.initialized)
                {
                    task1.SetResult(true);
                }
                else
                {
                    EventUtils.ListenOnce((_, _) => task1.TrySetResult(true),
                        h => this.Subscriber.Resubscribed += h,
                        h => this.Subscriber.Resubscribed -= h);
                }

                TaskCompletionSource<bool> task2 = new TaskCompletionSource<bool>();

                void RejectTransportOpen(object sender, EventArgs @event)
                {
                    task2.TrySetException(
                        new IOException("The transport was closed before the connection was established.")
                    );
                }

                async void Task2()
                {
                    var cleanupEvent = OnTransportClosed.ListenOnce(RejectTransportOpen);
                    try
                    {
                        var connectionTask = this.Provider.Connect();
                        if (ConnectionTimeout != null)
                            connectionTask = connectionTask.WithTimeout((TimeSpan)ConnectionTimeout, "socket stalled");

                        await connectionTask;
                        task2.TrySetResult(true);
                    }
                    finally
                    {
                        cleanupEvent();
                    }
                }

                Task2();

                await Task.WhenAll(task1.Task, task2.Task);
            }
            catch (Exception e)
            {
                // TODO Check for system socket hang up message
                if (e.Message != "socket stalled")
                    throw;

                this.OnTransportClosed?.Invoke(this, EventArgs.Empty);
            }
            finally
            {
                this.reconnecting = false;
            }
        }

        public async Task RestartTransport(string relayUrl = null, CancellationToken cancellationToken = default)
        {
            WCLogger.Log($"[Relayer] Restarting transport for {Name}. Explicitly closed: {_transportExplicitlyClosed}, reconnecting: {reconnecting}");

            if (_transportExplicitlyClosed || reconnecting)
            {
                return;
            }

            this.relayUrl = relayUrl ?? this.relayUrl;
            if (this.Connected)
            {
                TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();

                EventUtils.ListenOnce((_, _) => task1.TrySetResult(true),
                    h => this.Provider.Disconnected += h,
                    h => this.Provider.Disconnected -= h);

                await Task.WhenAll(task1.Task, TransportClose());
            }

            await CreateProvider();
            await TransportOpen();
        }

        void IRelayer.TriggerConnectionStalled()
        {
            this.OnConnectionStalled?.Invoke(this, EventArgs.Empty);
        }

        protected virtual void IsInitialized()
        {
            if (!initialized)
            {
                throw new InvalidOperationException($"{nameof(Relayer)} module not initialized.");
            }
        }

        private async Task ToEstablishConnection(CancellationToken cancellationToken = default)
        {
            if (Connected)
            {
                while (Provider.Connection.IsPaused && !Disposed)
                {
                    WCLogger.Log("[Relayer] Waiting for connection to unpause");
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }

                return;
            }

            if (Connecting)
            {
                // Check for connection
                while (Connecting && !Disposed)
                {
                    WCLogger.Log("[Relayer] Waiting for connection to open");
                    await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
                }

                if (!Connected && !Connecting)
                    throw new IOException("Could not establish connection");

                return;
            }

            await RestartTransport(cancellationToken: cancellationToken);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (Disposed) return;

            if (disposing)
            {
                _transportExplicitlyClosed = true;
                this.OnConnectionStalled -= OnConnectionStalledHandler;

                Subscriber?.Dispose();
                Publisher?.Dispose();
                Messages?.Dispose();
                
                // Un-listen to events
                Provider.Connected -= OnProviderConnected;
                Provider.Disconnected -= OnProviderDisconnected;
                Provider.RawMessageReceived -= OnProviderRawMessageReceived;
                Provider.ErrorReceived -= OnProviderErrorReceived;
                
                Provider?.Dispose();
            }

            Disposed = true;
        }
    }
}
