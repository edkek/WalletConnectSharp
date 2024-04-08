using Newtonsoft.Json;
using WalletConnectSharp.Common.Utils;

namespace WalletConnectSharp.Network.Models
{
    /// <summary>
    /// Represents a full JSON RPC request with the given parameter type T
    /// </summary>
    /// <typeparam name="T">The parameter type for this JSON RPC request</typeparam>
    public class JsonRpcRequest<T> : IJsonRpcRequest<T>
    {
        [JsonProperty("method")]
        private string _method;
        
        /// <summary>
        /// The method of this Json rpc request
        /// </summary>
        [JsonIgnore]
        public string Method
        {
            get => _method;
            set => _method = value;
        }

        [JsonProperty("params")]
        private T _params;

        /// <summary>
        /// The parameters of this Json rpc request
        /// </summary>
        [JsonIgnore]
        public T Params
        {
            get => _params;
            set => _params = value;
        }

        [JsonProperty("id")]
        private long _id;

        /// <summary>
        /// The id of this Json rpc request
        /// </summary>
        [JsonIgnore]
        public long Id
        {
            get => _id;
            set => _id = value;
        }

        [JsonProperty("jsonrpc")]
        private string _jsonRpc = "2.0";
        
        /// <summary>
        /// The JSON RPC version for this payload
        /// </summary>
        [JsonIgnore]
        public string JsonRPC => _jsonRpc;
        

        /// <summary>
        /// Create a blank Json rpc request
        /// </summary>
        [JsonConstructor]
        public JsonRpcRequest()
        {
        }

        /// <summary>
        /// Create a Json rpc request with the given method and parameter. You may
        /// optionally provide an id, if no id is given then a random id is generated.
        /// </summary>
        /// <param name="method">The method of this new Json rpc request</param>
        /// <param name="param">The parameter of this new Json rpc request</param>
        /// <param name="id">The id of this new Json rpc request</param>
        public JsonRpcRequest(string method, T param, long? id = null)
        {
            id ??= RpcPayloadId.Generate();

            this._method = method;
            this._params = param;
            this._id = (long)id;
        }
    }
}
