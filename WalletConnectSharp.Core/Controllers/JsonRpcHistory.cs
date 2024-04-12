using WalletConnectSharp.Common.Model.Errors;
using WalletConnectSharp.Core.Interfaces;
using WalletConnectSharp.Core.Models.History;
using WalletConnectSharp.Network;

namespace WalletConnectSharp.Core.Controllers
{
    /// <summary>
    /// A module that stores Json RPC request/response history data for a given Request type (T) and Response type (TR).
    /// Each request / response is stored in a JsonRpcRecord of type T, TR
    /// </summary>
    /// <typeparam name="T">The JSON RPC Request type</typeparam>
    /// <typeparam name="TR">The JSON RPC Response type</typeparam>
    public class JsonRpcHistory<T, TR> : IJsonRpcHistory<T, TR>
    {
        /// <summary>
        /// The storage version of this module
        /// </summary>
        public static readonly string Version = "0.3";

        /// <summary>
        /// The name of this module instance
        /// </summary>
        public string Name
        {
            get
            {
                return $"{_core.Name}-history-of-type-{typeof(T).FullName}-{typeof(TR).FullName}";
            }
        }

        /// <summary>
        /// The context string this module is using
        /// </summary>
        public string Context
        {
            get
            {
                return Name;
            }
        }

        /// <summary>
        /// The storage key this module uses to store data in the <see cref="ICore.Storage"/> module
        /// </summary>
        public string StorageKey
        {
            get
            {
                return WalletConnectCore.STORAGE_PREFIX + Version + "//" + Name;
            }
        }

        protected bool Disposed;

        private readonly Dictionary<long, JsonRpcRecord<T, TR>> _records = new();
        private readonly object _lock = new();
        private readonly ICore _core;

        private JsonRpcRecord<T, TR>[] _cached = [];
        private bool _initialized;

        public event EventHandler<JsonRpcRecord<T, TR>> Created;
        public event EventHandler<JsonRpcRecord<T, TR>> Updated;
        public event EventHandler<JsonRpcRecord<T, TR>> Deleted;
        public event EventHandler Sync;

        /// <summary>
        /// A mapping of Json RPC Records to their corresponding Json RPC id
        /// </summary>
        public IReadOnlyDictionary<long, JsonRpcRecord<T, TR>> Records
        {
            get
            {
                return _records;
            }
        }

        /// <summary>
        /// The number of history records stored
        /// </summary>
        public int Size
        {
            get
            {
                return _records.Count;
            }
        }

        /// <summary>
        /// An array of all JsonRpcRecord ids
        /// </summary>
        public long[] Keys
        {
            get
            {
                return _records.Keys.ToArray();
            }
        }

        /// <summary>
        /// An array of all JsonRpcRecords, each record contains a request / response
        /// </summary>
        public JsonRpcRecord<T, TR>[] Values
        {
            get
            {
                return _records.Values.ToArray();
            }
        }

        /// <summary>
        /// An array of all pending requests. A request is pending when it has no response
        /// </summary>
        public RequestEvent<T>[] Pending
        {
            get
            {
                var pending = Values.Where(jrr => jrr.Response == null);

                return pending.Select(RequestEvent<T>.FromPending).ToArray();
            }
        }

        public JsonRpcHistory(ICore core)
        {
            _core = core;
        }

        /// <summary>
        /// Initialize this JsonRpcFactory. This will restore all history records from storage
        /// </summary>
        /// <returns></returns>
        public async Task Init()
        {
            if (!_initialized)
            {
                await Restore();
                foreach (var record in _cached)
                {
                    _records.Add(record.Id, record);
                }

                _cached = Array.Empty<JsonRpcRecord<T, TR>>();
                RegisterEventListeners();
                _initialized = true;
            }
        }

        /// <summary>
        /// Set a new request in the given topic on the given chainId. This will add the request to the
        /// history as pending. To add a response to this request, use the <see cref="Resolve"/> method
        /// </summary>
        /// <param name="topic">The topic to record this request in</param>
        /// <param name="request">The request to record</param>
        /// <param name="chainId">The chainId this request came from</param>
        public void Set(string topic, IJsonRpcRequest<T> request, string chainId)
        {
            IsInitialized();

            JsonRpcRecord<T, TR> record;

            lock (_lock)
            {
                if (_records.ContainsKey(request.Id))
                {
                    return;
                }

                record = new JsonRpcRecord<T, TR>(request) { Id = request.Id, Topic = topic, ChainId = chainId };
                _records.Add(record.Id, record);
            }
   
            this.Created?.Invoke(this, record);
        }

        /// <summary>
        /// Get a request that has previously been set with a given topic and id.
        /// </summary>
        /// <param name="topic">The topic of the request was made in</param>
        /// <param name="id">The id of the request to get</param>
        /// <returns>The recorded request record</returns>
        public Task<JsonRpcRecord<T, TR>> Get(string topic, long id)
        {
            IsInitialized();

            var record = GetRecord(id);

            // TODO Log
            /*if (topic != record.Topic)
            {
                throw WalletConnectException.FromType(ErrorType.MISMATCHED_TOPIC, $"{Name}: {id}");
            }*/

            return Task.FromResult<JsonRpcRecord<T, TR>>(record);
        }

        /// <summary>
        /// Resolve a request that has previously been set using a specific response. The id and topic of the response
        /// will be used to determine which request to resolve. If the request is not found, then nothing happens.
        /// </summary>
        /// <param name="response">The response to resolve. The id and topic of the response
        /// will be used to determine which request to resolve.</param>
        /// <returns></returns>
        public Task Resolve(IJsonRpcResult<TR> response)
        {
            IsInitialized();
            if (!_records.ContainsKey(response.Id)) return Task.CompletedTask;

            var record = GetRecord(response.Id);
            if (record.Response != null) return Task.CompletedTask;

            record.Response = response;
            this.Updated?.Invoke(this, record);
            return Task.CompletedTask;
        }

        /// <summary>
        /// Delete a request record with a given topic and id (optional). If the request is not found, then nothing happens.
        /// </summary>
        /// <param name="topic">The topic the request was made in</param>
        /// <param name="id">The id of the request. If no id is given then all requests in the given topic are deleted.</param>
        public void Delete(string topic, long? id = null)
        {
            IsInitialized();

            foreach (var record in Values)
            {
                if (record.Topic == topic)
                {
                    if (id != null && record.Id != id) continue;
                    _records.Remove(record.Id);
                    this.Deleted?.Invoke(this, record);
                }
            }
        }

        /// <summary>
        /// Check if a request with a given topic and id exists.
        /// </summary>
        /// <param name="topic">The topic the request was made in</param>
        /// <param name="id">The id of the request</param>
        /// <returns>True if the request with the given topic and id exists, false otherwise</returns>
        public Task<bool> Exists(string topic, long id)
        {
            try
            {
                IsInitialized();
                if (_records.ContainsKey(id)) return Task.FromResult<bool>(false);
                var record = GetRecord(id);

                return Task.FromResult(record.Topic == topic);
            }
            catch (WalletConnectException e)
            {
                if (e.CodeType == ErrorType.NO_MATCHING_KEY)
                    return Task.FromResult(false);
                throw;
            }
        }

        private Task SetJsonRpcRecords(JsonRpcRecord<T, TR>[] records)
        {
            return _core.Storage.SetItem(StorageKey, records);
        }

        private async Task<JsonRpcRecord<T, TR>[]> GetJsonRpcRecords()
        {
            if (await _core.Storage.HasItem(StorageKey))
                return await _core.Storage.GetItem<JsonRpcRecord<T, TR>[]>(StorageKey);

            return Array.Empty<JsonRpcRecord<T, TR>>();
        }

        private JsonRpcRecord<T, TR> GetRecord(long id)
        {
            IsInitialized();

            if (!_records.ContainsKey(id))
            {
                throw WalletConnectException.FromType(ErrorType.NO_MATCHING_KEY,
                    new Dictionary<string, object>() { { "Tag", $"{Name}: {id}" } });
            }

            return _records[id];
        }

        private async Task Persist()
        {
            await SetJsonRpcRecords(Values);
            this.Sync?.Invoke(this, EventArgs.Empty);
        }

        private async Task Restore()
        {
            var persisted = await GetJsonRpcRecords();
            if (persisted == null)
                return;
            if (persisted.Length == 0)
                return;
            if (_records.Count > 0)
            {
                throw WalletConnectException.FromType(ErrorType.RESTORE_WILL_OVERRIDE, Name);
            }

            _cached = persisted;
        }

        private void RegisterEventListeners()
        {
            this.Created += SaveRecordCallback;
            this.Updated += SaveRecordCallback;
            this.Deleted += SaveRecordCallback;
        }

        private async void SaveRecordCallback(object sender, JsonRpcRecord<T, TR> @event)
        {
            await Persist();
        }

        private void IsInitialized()
        {
            if (!_initialized)
            {
                throw WalletConnectException.FromType(ErrorType.NOT_INITIALIZED, Name);
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
                this.Created -= SaveRecordCallback;
                this.Updated -= SaveRecordCallback;
                this.Deleted -= SaveRecordCallback;
            }

            Disposed = true;
        }
    }
}
