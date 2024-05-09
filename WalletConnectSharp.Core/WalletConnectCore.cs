using WalletConnectSharp.Core.Controllers;
using WalletConnectSharp.Core.Interfaces;
using WalletConnectSharp.Core.Models;
using WalletConnectSharp.Core.Models.Relay;
using WalletConnectSharp.Core.Models.Verify;
using WalletConnectSharp.Crypto;
using WalletConnectSharp.Crypto.Interfaces;
using WalletConnectSharp.Network;
using WalletConnectSharp.Storage;
using WalletConnectSharp.Storage.Interfaces;
using WalletConnectSharp.Network.Websocket;

namespace WalletConnectSharp.Core
{
    /// <summary>
    /// The Core module. This module holds all Core Modules and holds configuration data
    /// required by several Core Module.
    /// </summary>
    public class WalletConnectCore : ICore
    {
        /// <summary>
        /// The prefix string used for the storage key
        /// </summary>
        public static readonly string STORAGE_PREFIX = ICore.Protocol + "@" + ICore.Version + ":core:";

        private string _optName;

        /// <summary>
        /// The name of this module. 
        /// </summary>
        public string Name
        {
            get
            {
                return $"{_optName}-core";
            }
        }

        private string guid = "";

        /// <summary>
        /// The current context of this module instance. 
        /// </summary>
        public string Context
        {
            get
            {
                return $"{Name}{guid}";
            }
        }

        /// <summary>
        /// If this module is initialized or not
        /// </summary>
        public bool Initialized { get; private set; }

        /// <summary>
        /// The url of the relay server to connect to in the <see cref="IRelayer"/> module
        /// </summary>
        public string RelayUrl { get; }

        /// <summary>
        /// The Project ID to use for authentication on the relay server
        /// </summary>
        public string ProjectId { get; }

        /// <summary>
        /// The <see cref="IHeartBeat"/> module this Core module is using
        /// </summary>
        public IHeartBeat HeartBeat { get; }

        /// <summary>
        /// The <see cref="ICrypto"/> module this Core module is using
        /// </summary>
        public ICrypto Crypto { get; }

        /// <summary>
        /// The <see cref="IRelayer"/> module this Core module is using
        /// </summary>
        public IRelayer Relayer { get; }

        /// <summary>
        /// The <see cref="IKeyValueStorage"/> module this Core module is using. All
        /// Core Modules should use this for storage.
        /// </summary>
        public IKeyValueStorage Storage { get; }

        /// <summary>
        /// The <see cref="ITypedMessageHandler"/> module this Core module is using. Use this for handling
        /// custom message types (request or response) and for sending messages (request, responses or errors)
        /// </summary>
        public ITypedMessageHandler MessageHandler { get; }

        /// <summary>
        /// The <see cref="IExpirer"/> module this Sign Client is using to track expiration dates
        /// </summary>
        public IExpirer Expirer { get; }

        /// <summary>
        /// The <see cref="IJsonRpcHistoryFactory"/> factory this Sign Client module is using. Used for storing
        /// JSON RPC request and responses of various types T, TR
        /// </summary>
        public IJsonRpcHistoryFactory History { get; }

        /// <summary>
        /// The <see cref="IPairing"/> module this Core module is using. Used for pairing two peers
        /// with each other and keeping track of pairing state
        /// </summary>
        public IPairing Pairing { get; }

        public Verifier Verify { get; }

        public CoreOptions Options { get; }

        public bool Disposed { get; protected set; }

        /// <summary>
        /// Create a new Core with the given options.
        /// </summary>
        /// <param name="options">The options to use to configure the new Core module</param>
        public WalletConnectCore(CoreOptions options = null)
        {
            if (options == null)
            {
                var storage = new InMemoryStorage();
                options = new CoreOptions()
                {
                    KeyChain = new KeyChain(storage), ProjectId = null, RelayUrl = null, Storage = storage
                };
            }

            if (options.Storage == null)
            {
                options.Storage = new FileSystemStorage();
            }


            options.ConnectionBuilder ??= new WebsocketConnectionBuilder();
            options.RelayUrlBuilder ??= new RelayUrlBuilder();

            Options = options;
            ProjectId = options.ProjectId;
            RelayUrl = options.RelayUrl;
            Storage = options.Storage;

            if (options.CryptoModule != null)
            {
                Crypto = options.CryptoModule;
            }
            else
            {
                if (options.KeyChain == null)
                {
                    options.KeyChain = new KeyChain(options.Storage);
                }

                Crypto = new Crypto.Crypto(options.KeyChain);
            }

            HeartBeat = new HeartBeat();
            _optName = options.Name;

            Expirer = new Expirer(this);
            Pairing = new Pairing(this);
            Verify = new Verifier();

            Relayer = new Relayer(new RelayerOptions()
            {
                Core = this,
                ProjectId = ProjectId,
                RelayUrl = options.RelayUrl,
                ConnectionTimeout = options.ConnectionTimeout,
                RelayUrlBuilder = options.RelayUrlBuilder
            });

            MessageHandler = new TypedMessageHandler(this);
            History = new JsonRpcHistoryFactory(this);
        }

        /// <summary>
        /// Start this module, this will initialize all Core Modules. If this module has already been
        /// initialized, then nothing will happen
        /// </summary>
        public async Task Start()
        {
            if (Initialized) return;

            Initialized = true;
            await Initialize();
        }

        private async Task Initialize()
        {
            await Storage.Init();
            await Crypto.Init();
            await Relayer.Init();
            await HeartBeat.InitAsync();
            await Expirer.Init();
            await MessageHandler.Init();
            await Pairing.Init();
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
                HeartBeat?.Dispose();
                Crypto?.Dispose();
                Relayer?.Dispose();
                Storage?.Dispose();
                MessageHandler?.Dispose();
                Expirer?.Dispose();
                Pairing?.Dispose();
                Verify?.Dispose();
            }

            Disposed = true;
        }
    }
}
