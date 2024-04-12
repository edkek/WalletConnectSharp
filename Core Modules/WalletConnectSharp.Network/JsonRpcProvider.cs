using Newtonsoft.Json;
using WalletConnectSharp.Common;
using WalletConnectSharp.Common.Events;
using WalletConnectSharp.Common.Logging;
using WalletConnectSharp.Common.Model.Errors;
using WalletConnectSharp.Network.Models;

namespace WalletConnectSharp.Network
{
    /// <summary>
    /// A full implementation of the IJsonRpcProvider interface using the EventDelegator
    /// </summary>
    public class JsonRpcProvider : IJsonRpcProvider, IModule
    {
        private IJsonRpcConnection _connection;
        private bool _hasRegisteredEventListeners;
        private Guid _context;
        private bool _connectingStarted;
        private TaskCompletionSource<bool> _connecting = new();
        private long _lastId;

        public event EventHandler<JsonRpcPayload> PayloadReceived;

        public event EventHandler<IJsonRpcConnection> Connected;

        public event EventHandler Disconnected;

        public event EventHandler<Exception> ErrorReceived;

        public event EventHandler<string> RawMessageReceived;

        private readonly GenericEventHolder _jsonResponseEventHolder = new();
        protected bool Disposed;

        /// <summary>
        /// Whether the provider is currently connecting or not
        /// </summary>
        public bool IsConnecting => _connectingStarted && !_connecting.Task.IsCompleted;

        /// <summary>
        /// The current Connection for this provider
        /// </summary>
        public IJsonRpcConnection Connection
        {
            get
            {
                return _connection;
            }
        }

        /// <summary>
        /// The name of this provider module
        /// </summary>
        public string Name
        {
            get
            {
                return "json-rpc-provider";
            }
        }

        /// <summary>
        /// The context string of this provider module
        /// </summary>
        public string Context
        {
            get
            {
                //TODO Get context from logger
                return _context.ToString();
            }
        }

        /// <summary>
        /// Create a new JsonRpcProvider with the given connection
        /// </summary>
        /// <param name="connection">The IJsonRpcConnection to use</param>
        public JsonRpcProvider(IJsonRpcConnection connection)
        {
            _context = Guid.NewGuid();
            this._connection = connection;
            if (this._connection.Connected)
            {
                RegisterEventListeners();
            }
        }

        /// <summary>
        /// Connect this provider using the given connection string
        /// </summary>
        /// <param name="connection">The connection string to use to connect, usually a URI</param>
        public async Task Connect(string connection)
        {
            if (this._connection.Connected)
            {
                await this._connection.Close();
            }

            // Reset connecting task
            _connecting = new TaskCompletionSource<bool>();
            _connectingStarted = true;

            await this._connection.Open(connection);

            FinalizeConnection(this._connection);
        }

        /// <summary>
        /// Connect this provider with the given IJsonRpcConnection connection
        /// </summary>
        /// <param name="connection">The connection object to use to connect</param>
        public async Task Connect(IJsonRpcConnection connection)
        {
            if (this._connection.Url == connection.Url && connection.Connected) return;
            if (this._connection.Connected)
            {
                await this._connection.Close();
            }

            // Reset connecting task
            _connecting = new TaskCompletionSource<bool>();
            _connectingStarted = true;

            void OnConnectionOnOpened(object o, object o1)
            {
                _connecting.SetResult(true);
            }

            void OnConnectionErrored(object o, Exception e)
            {
                if (_connecting.Task.IsCompleted)
                {
                    return;
                }

                _connecting.SetException(e);
            }

            connection.Opened += OnConnectionOnOpened;
            connection.ErrorReceived += OnConnectionErrored;

            await connection.Open();

            try
            {
                await _connecting.Task;
            }
            catch (Exception)
            {
                _connectingStarted = false;
                throw;
            }
            finally
            {
                connection.Opened -= OnConnectionOnOpened;
                connection.ErrorReceived -= OnConnectionErrored;
            }
            
            FinalizeConnection(connection);
        }

        private void FinalizeConnection(IJsonRpcConnection connection)
        {
            this._connection = connection;
            RegisterEventListeners();
            this.Connected?.Invoke(this, connection);
            _connectingStarted = false;
        }

        /// <summary>
        /// Connect this provider using the backing IJsonRpcConnection that was set in the
        /// constructor
        /// </summary>
        public async Task Connect()
        {
            if (_connection == null)
                throw new Exception("No connection is set");

            await Connect(_connection);
        }

        /// <summary>
        /// Disconnect this provider
        /// </summary>
        public async Task Disconnect()
        {
            await _connection.Close();
            // Reset connecting task
            _connecting = new TaskCompletionSource<bool>();
        }

        /// <summary>
        /// Send a request and wait for a response. The response is returned as a task and can
        /// be awaited
        /// </summary>
        /// <param name="requestArgs">The request arguments to send</param>
        /// <param name="context">The context to use during sending</param>
        /// <typeparam name="T">The type of request arguments</typeparam>
        /// <typeparam name="TR">The type of the response</typeparam>
        /// <returns>A Task that will resolve when a response is received</returns>
        public async Task<TR> Request<T, TR>(IRequestArguments<T> requestArgs, object context = null)
        {
            WCLogger.Log("[JsonRpcProvider] Checking if connected");
            if (IsConnecting)
            {
                await _connecting.Task;
            }
            else if (!_connectingStarted && !_connection.Connected)
            {
                WCLogger.Log("[JsonRpcProvider] Not connected, connecting now");
                await Connect(_connection);
            }

            long? id = null;
            if (requestArgs is IJsonRpcRequest<T>)
            {
                id = ((IJsonRpcRequest<T>)requestArgs).Id;
                if (id == 0)
                    id = null; // An id of 0 is null
            }

            var request = new JsonRpcRequest<T>(requestArgs.Method, requestArgs.Params, id);

            TaskCompletionSource<TR> requestTask = new TaskCompletionSource<TR>(TaskCreationOptions.None);

            _jsonResponseEventHolder.OfType<string>()[request.Id.ToString()] += (sender, responseJson) =>
            {
                if (requestTask.Task.IsCompleted)
                    return;

                var result = JsonConvert.DeserializeObject<JsonRpcResponse<TR>>(responseJson);

                if (result.Error != null)
                {
                    requestTask.SetException(new IOException(result.Error.Message));
                }
                else
                {
                    requestTask.SetResult(result.Result);
                }
            };

            _jsonResponseEventHolder.OfType<WalletConnectException>()[request.Id.ToString()] += (sender, exception) =>
            {
                if (requestTask.Task.IsCompleted)
                    return;

                if (exception != null)
                {
                    requestTask.SetException(exception);
                }
            };

            _connection.ErrorReceived += (_, exception) =>
            {
                if (requestTask.Task.IsCompleted)
                {
                    return;
                }

                requestTask.SetException(exception);
            };

            _lastId = request.Id;

            WCLogger.Log(
                $"[JsonRpcProvider] Sending request {request.Method} with data {JsonConvert.SerializeObject(request)}");
            await _connection.SendRequest(request, context);

            await requestTask.Task;

            return requestTask.Task.Result;
        }

        protected void RegisterEventListeners()
        {
            if (_hasRegisteredEventListeners) return;
            
            _connection.PayloadReceived += OnPayload;
            _connection.Closed += OnConnectionDisconnected;
            _connection.ErrorReceived += OnConnectionError;

            _hasRegisteredEventListeners = true;
        }

        protected void UnregisterEventListeners()
        {
            if (!_hasRegisteredEventListeners) return;

            _connection.PayloadReceived -= OnPayload;
            _connection.Closed -= OnConnectionDisconnected;
            _connection.ErrorReceived -= OnConnectionError;

            _hasRegisteredEventListeners = false;
        }

        private void OnConnectionError(object sender, Exception e)
        {
            this.ErrorReceived?.Invoke(this, e);
        }

        private void OnConnectionDisconnected(object sender, EventArgs e)
        {
            this.Disconnected?.Invoke(this, e);
        }

        private void OnPayload(object sender, string json)
        {
            var payload = JsonConvert.DeserializeObject<JsonRpcPayload>(json);

            if (payload == null)
            {
                throw new IOException("Invalid payload: " + json);
            }

            if (payload.Id == 0)
                payload.Id = _lastId;
            
            this.PayloadReceived?.Invoke(this, payload);

            if (payload.IsRequest)
            {
                this.RawMessageReceived?.Invoke(this, json);
            }
            else
            {
                if (payload.IsError)
                {
                    var errorPayload = JsonConvert.DeserializeObject<JsonRpcError>(json);
                    _jsonResponseEventHolder.OfType<WalletConnectException>()[payload.Id.ToString()](this,
                        errorPayload.Error.ToException());
                }
                else
                {
                    _jsonResponseEventHolder.OfType<string>()[payload.Id.ToString()](this, json);
                }
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
                UnregisterEventListeners();
                _connection?.Dispose();
            }

            Disposed = true;
        }
    }
}
