using System.Security.Cryptography;
using System.Text.RegularExpressions;
using WalletConnectSharp.Common.Events;
using WalletConnectSharp.Common.Logging;
using WalletConnectSharp.Common.Model.Errors;
using WalletConnectSharp.Common.Model.Relay;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Core.Interfaces;
using WalletConnectSharp.Core.Models;
using WalletConnectSharp.Core.Models.Expirer;
using WalletConnectSharp.Core.Models.Pairing;
using WalletConnectSharp.Core.Models.Pairing.Methods;
using WalletConnectSharp.Core.Models.Relay;
using WalletConnectSharp.Network.Models;

namespace WalletConnectSharp.Core.Controllers
{
    /// <summary>
    /// A module that handles pairing two peers and storing related data
    /// </summary>
    public class Pairing : IPairing
    {
        protected bool Disposed;

        private const int KeyLength = 32;
        private bool _initialized;
        private HashSet<string> _registeredMethods = new HashSet<string>();

        /// <summary>
        /// The name for this module instance
        /// </summary>
        public string Name
        {
            get
            {
                return $"{Core.Context}-pairing";
            }
        }

        /// <summary>
        /// The context string for this Pairing module
        /// </summary>
        public string Context
        {
            get
            {
                return Name;
            }
        }

        public event EventHandler<PairingEvent> PairingExpired;
        public event EventHandler<PairingEvent> PairingPinged;
        public event EventHandler<PairingEvent> PairingDeleted;

        private EventHandlerMap<JsonRpcResponse<bool>> PairingPingResponseEvents = new();
        private DisposeHandlerToken pairingDeleteMessageHandler;
        private DisposeHandlerToken pairingPingMessageHandler;

        /// <summary>
        /// Get the <see cref="IStore{TKey,TValue}"/> module that is handling the storage of
        /// <see cref="PairingStruct"/> 
        /// </summary>
        public IPairingStore Store { get; }

        /// <summary>
        /// Get all active and inactive pairings
        /// </summary>
        public PairingStruct[] Pairings
        {
            get
            {
                return Store.Values;
            }
        }

        /// <summary>
        /// The <see cref="ICore"/> module using this module instance
        /// </summary>
        public ICore Core { get; }

        /// <summary>
        /// Create a new instance of the Pairing module using the given <see cref="ICore"/> module
        /// </summary>
        /// <param name="core">The <see cref="ICore"/> module that is using this new Pairing module</param>
        public Pairing(ICore core)
        {
            this.Core = core;
            this.Store = new PairingStore(core);
        }

        /// <summary>
        /// Initialize this pairing module. This will restore all active / inactive pairings
        /// from storage
        /// </summary>
        public async Task Init()
        {
            if (!_initialized)
            {
                await this.Store.Init();
                await Cleanup();
                await RegisterTypedMessages();
                RegisterExpirerEvents();
                this._initialized = true;
            }
        }

        private void RegisterExpirerEvents()
        {
            this.Core.Expirer.Expired += ExpiredCallback;
        }

        private async Task RegisterTypedMessages()
        {
            this.pairingDeleteMessageHandler = await Core.MessageHandler.HandleMessageType<PairingDelete, bool>(OnPairingDeleteRequest, null);
            this.pairingPingMessageHandler = await Core.MessageHandler.HandleMessageType<PairingPing, bool>(OnPairingPingRequest, OnPairingPingResponse);
        }

        /// <summary>
        /// Pair with a peer using the given uri. The uri must be in the correct
        /// format otherwise an exception will be thrown. You may (optionally) pair
        /// without activating the pairing. By default the pairing will be activated before
        /// it is returned
        /// </summary>
        /// <param name="uri">The URI to pair with</param>
        /// <returns>The pairing data that can be used to pair with the peer</returns>
        public async Task<PairingStruct> Pair(string uri, bool activatePairing = true)
        {
            IsInitialized();
            ValidateUri(uri);
            
            var uriParams = ParseUri(uri);

            var topic = uriParams.Topic;
            var symKey = uriParams.SymKey;
            var relay = uriParams.Relay;

            if (this.Store.Keys.Contains(topic))
            {
                throw new ArgumentException($"Topic {topic} already has pairing");
            }

            if (await this.Core.Crypto.HasKeys(topic))
            {
                throw new ArgumentException($"Topic {topic} already has keychain");
            }

            var expiry = Clock.CalculateExpiry(Clock.FIVE_MINUTES);
            var pairing = new PairingStruct()
            {
                Topic = topic, Relay = relay, Expiry = expiry, Active = false,
            };

            await this.Store.Set(topic, pairing);
            await this.Core.Crypto.SetSymKey(symKey, topic);
            await this.Core.Relayer.Subscribe(topic, new SubscribeOptions() { Relay = relay });

            this.Core.Expirer.Set(topic, expiry);

            if (activatePairing)
            {
                await ActivatePairing(topic);
            }

            return pairing;
        }

        /// <summary>
        /// Parse a session proposal URI and return all information in the URI in a
        /// new <see cref="UriParameters"/> object
        /// </summary>
        /// <param name="uri">The uri to parse</param>
        /// <returns>A new <see cref="UriParameters"/> object that contains all data
        /// parsed from the given uri</returns>
        public UriParameters ParseUri(string uri)
        {
            var pathStart = uri.IndexOf(":", StringComparison.Ordinal);
            int? pathEnd = uri.IndexOf("?", StringComparison.Ordinal) != -1
                ? uri.IndexOf("?", StringComparison.Ordinal)
                : null;
            var protocol = uri.Substring(0, pathStart);

            string path;
            if (pathEnd != null) path = uri.Substring(pathStart + 1, (int)pathEnd - (pathStart + 1));
            else path = uri.Substring(pathStart + 1);

            var requiredValues = path.Split("@");
            var queryString = pathEnd != null ? uri[(int)pathEnd..] : "";
            var queryParams = Regex
                .Matches(queryString, "([^?=&]+)(=([^&]*))?")
                .ToDictionary(x => x.Groups[1].Value, x => x.Groups[3].Value);

            var result = new UriParameters()
            {
                Protocol = protocol,
                Topic = requiredValues[0],
                Version = int.Parse(requiredValues[1]),
                SymKey = queryParams["symKey"],
                Relay = new ProtocolOptions()
                {
                    Protocol = queryParams["relay-protocol"], Data = queryParams.GetValueOrDefault("relay-data")
                }
            };

            return result;
        }

        /// <summary>
        /// Create a new pairing at the given pairing topic
        /// </summary>
        /// <returns>A new instance of <see cref="CreatePairingData"/> that includes the pairing topic and
        /// uri</returns>
        public async Task<CreatePairingData> Create()
        {
            byte[] symKeyRaw = new byte[KeyLength];
            RandomNumberGenerator.Fill(symKeyRaw);
            var symKey = symKeyRaw.ToHex();
            var topic = await this.Core.Crypto.SetSymKey(symKey);
            var expiry = Clock.CalculateExpiry(Clock.FIVE_MINUTES);
            var relay = new ProtocolOptions() { Protocol = RelayProtocols.Default };
            var pairing = new PairingStruct()
            {
                Topic = topic, Expiry = expiry, Relay = relay, Active = false,
            };
            var uri = $"{ICore.Protocol}:{topic}@{ICore.Version}?"
                .AddQueryParam("symKey", symKey)
                .AddQueryParam("relay-protocol", relay.Protocol);

            if (!string.IsNullOrWhiteSpace(relay.Data))
                uri = uri.AddQueryParam("relay-data", relay.Data);

            await this.Store.Set(topic, pairing);
            await this.Core.Relayer.Subscribe(topic);
            this.Core.Expirer.Set(topic, expiry);

            return new CreatePairingData() { Topic = topic, Uri = uri };
        }

        /// <summary>
        /// Activate a previously created pairing at the given topic
        /// </summary>
        /// <param name="topic">The topic of the pairing to activate</param>
        public Task Activate(string topic)
        {
            return ActivatePairing(topic);
        }

        /// <summary>
        /// Subscribe to method requests
        /// </summary>
        /// <param name="methods">The methods to register and subscribe</param>
        public Task Register(string[] methods)
        {
            IsInitialized();
            foreach (var method in methods)
            {
                _registeredMethods.Add(method);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Update the expiration of an existing pairing at the given topic
        /// </summary>
        /// <param name="topic">The topic of the pairing to update</param>
        /// <param name="expiration">The new expiration date as a unix timestamp (seconds)</param>
        /// <returns></returns>
        public Task UpdateExpiry(string topic, long expiration)
        {
            IsInitialized();
            return this.Store.Update(topic, new PairingStruct() { Expiry = expiration });
        }

        /// <summary>
        /// Update the metadata of an existing pairing at the given topic
        /// </summary>
        /// <param name="topic">The topic of the pairing to update</param>
        /// <param name="metadata">The new metadata</param>
        public Task UpdateMetadata(string topic, Metadata metadata)
        {
            IsInitialized();
            return this.Store.Update(topic, new PairingStruct() { PeerMetadata = metadata });
        }

        /// <summary>
        /// Ping an existing pairing at the given topic
        /// </summary>
        /// <param name="topic">The topic of the pairing to ping</param>
        public async Task Ping(string topic)
        {
            IsInitialized();
            await IsValidPairingTopic(topic);
            if (this.Store.Keys.Contains(topic))
            {
                var id = await Core.MessageHandler.SendRequest<PairingPing, bool>(topic, new PairingPing());
                var done = new TaskCompletionSource<bool>();

                PairingPingResponseEvents.ListenOnce($"pairing_ping{id}", (sender, args) =>
                {
                    if (args.IsError)
                        done.SetException(args.Error.ToException());
                    else
                        done.SetResult(args.Result);
                });

                await done.Task;
            }
        }

        /// <summary>
        /// Disconnect an existing pairing at the given topic
        /// </summary>
        /// <param name="topic">The topic of the pairing to disconnect</param>
        public async Task Disconnect(string topic)
        {
            IsInitialized();
            await IsValidPairingTopic(topic);

            if (Store.Keys.Contains(topic))
            {
                var error = Error.FromErrorType(ErrorType.USER_DISCONNECTED);
                await Core.MessageHandler.SendRequest<PairingDelete, bool>(topic,
                    new PairingDelete() { Code = error.Code, Message = error.Message });
                await DeletePairing(topic);
            }
        }

        private async Task ActivatePairing(string topic)
        {
            var expiry = Clock.CalculateExpiry(Clock.THIRTY_DAYS);
            await this.Store.Update(topic, new PairingStruct() { Active = true, Expiry = expiry });

            this.Core.Expirer.Set(topic, expiry);
        }

        private async Task DeletePairing(string topic)
        {
            bool expirerHasDeleted = !this.Core.Expirer.Has(topic);
            bool pairingHasDeleted = !this.Store.Keys.Contains(topic);
            bool symKeyHasDeleted = !(await this.Core.Crypto.HasKeys(topic));

            await this.Core.Relayer.Unsubscribe(topic);
            await Task.WhenAll(
                pairingHasDeleted
                    ? Task.CompletedTask
                    : this.Store.Delete(topic, Error.FromErrorType(ErrorType.USER_DISCONNECTED)),
                symKeyHasDeleted ? Task.CompletedTask : this.Core.Crypto.DeleteSymKey(topic),
                expirerHasDeleted ? Task.CompletedTask : this.Core.Expirer.Delete(topic)
            );
        }

        private Task Cleanup()
        {
            List<string> pairingTopics = (from pair in this.Store.Values.Where(e => e.Expiry != null)
                where pair.Expiry != null && Clock.IsExpired(pair.Expiry.Value)
                select pair.Topic).ToList();

            return Task.WhenAll(
                pairingTopics.Select(DeletePairing)
            );
        }

        private async Task IsValidPairingTopic(string topic)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                throw new ArgumentNullException(nameof(topic));
            }

            if (!this.Store.Keys.Contains(topic))
                throw new KeyNotFoundException($"Pairing topic {topic} not found.");

            var expiry = this.Store.Get(topic).Expiry;
            if (expiry != null && Clock.IsExpired(expiry.Value))
            {
                await DeletePairing(topic);
                throw new ExpiredException($"Pairing topic {topic} has expired.");
            }
        }

        private static bool IsValidUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;

            try
            {
                new Uri(url);
                return true;
            }
            catch (Exception e)
            {
                return false;
            }
        }

        private void ValidateUri(string uri)
        {
            if (!IsValidUrl(uri))
                throw new FormatException($"Invalid URI format: {uri}");
        }

        private void IsInitialized()
        {
            if (!_initialized)
            {
                throw new InvalidOperationException($"{nameof(Pairing)} module not initialized.");
            }
        }

        private async Task OnPairingPingRequest(string topic, JsonRpcRequest<PairingPing> payload)
        {
            var id = payload.Id;
            try
            {
                await IsValidPairingTopic(topic);

                await Core.MessageHandler.SendResult<PairingPing, bool>(id, topic, true);
                this.PairingPinged?.Invoke(this, new PairingEvent() { Topic = topic, Id = id });
            }
            catch (WalletConnectException e)
            {
                await Core.MessageHandler.SendError<PairingPing, bool>(id, topic, Error.FromException(e));
            }
        }

        private async Task OnPairingPingResponse(string topic, JsonRpcResponse<bool> payload)
        {
            var id = payload.Id;

            // put at the end of the stack to avoid a race condition
            // where session_ping listener is not yet initialized
            await Task.Delay(500);

            this.PairingPinged?.Invoke(this, new PairingEvent() { Id = id, Topic = topic });

            PairingPingResponseEvents[$"pairing_ping{id}"](this, payload);
        }

        private async Task OnPairingDeleteRequest(string topic, JsonRpcRequest<PairingDelete> payload)
        {
            var id = payload.Id;
            try
            {
                await IsValidDisconnect(topic, payload.Params);

                await Core.MessageHandler.SendResult<PairingDelete, bool>(id, topic, true);
                await DeletePairing(topic);
                this.PairingDeleted?.Invoke(this, new PairingEvent() { Topic = topic, Id = id });
            }
            catch (WalletConnectException e)
            {
                await Core.MessageHandler.SendError<PairingDelete, bool>(id, topic, Error.FromException(e));
            }
        }

        private async Task IsValidDisconnect(string topic, Error reason)
        {
            if (string.IsNullOrWhiteSpace(topic))
            {
                throw new ArgumentNullException(nameof(topic));
            }

            await IsValidPairingTopic(topic);
        }

        private async void ExpiredCallback(object sender, ExpirerEventArgs e)
        {
            WCLogger.Log($"Expired topic {e.Target}");
            var target = new ExpirerTarget(e.Target);

            if (string.IsNullOrWhiteSpace(target.Topic)) return;

            var topic = target.Topic;
            if (this.Store.Keys.Contains(topic))
            {
                await DeletePairing(topic);
                this.PairingExpired?.Invoke(this, new PairingEvent() { Topic = topic, });
            }
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
                Store?.Dispose();
                this.pairingDeleteMessageHandler.Dispose();
                this.pairingPingMessageHandler.Dispose();
            }

            Disposed = true;
        }
    }
}
