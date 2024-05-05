using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WalletConnectSharp.Network.Models
{
    /// <summary>
    /// Represents a generic JSON RPC payload that may be a response/request/error, with properties to determine
    /// which it is
    /// </summary>
    public class JsonRpcPayload : IJsonRpcPayload
    {
        [JsonProperty("id")]
        private long _id;
        
        [JsonProperty("jsonrpc")]
        private string _jsonRPC = "2.0";

        /// <summary>
        /// The JSON RPC id for this payload
        /// </summary>
        [JsonIgnore]
        public long Id
        {
            get => _id;
            set => _id = value;
        }

        /// <summary>
        /// The JSON RPC version for this payload
        /// </summary>
        [JsonIgnore]
        public string JsonRPC => _jsonRPC;
        
        [JsonExtensionData]
#pragma warning disable CS0649
        private IDictionary<string, JToken> _extraStuff;
#pragma warning restore CS0649

        /// <summary>
        /// Get the method for this payload, if this payload is a request.
        /// If this payload is not a request, then an error is thrown
        /// If the payload contains a "params" object and within it a "request" object with a "method" value, that method is returned.
        /// Otherwise, the method is retrieved directly from the payload's "method" value.
        /// </summary>
        [JsonIgnore]
        public string Method
        {
            get
            {
                if (!IsRequest)
                    throw new ArgumentException("The given payload is not a request, and thus has no Method");

                if (_extraStuff.TryGetValue("params", out var param) && param["request"]?["method"] != null)
                {
                    return param["request"]?["method"].ToObject<string>();
                }
                
                var method = _extraStuff["method"].ToObject<string>();
                return method;
            }
        }
        
        /// <summary>
        /// Whether this payload represents a request
        /// </summary>+
        [JsonIgnore]
        public bool IsRequest
        {
            get
            {
                return _extraStuff.ContainsKey("method");
            }
        }

        /// <summary>
        /// Whether this payload represents a response
        /// </summary>
        [JsonIgnore]
        public bool IsResponse
        {
            get
            {
                return _extraStuff.ContainsKey("result") || IsError;
            }
        }

        /// <summary>
        /// Whether this payload represents an error
        /// </summary>
        [JsonIgnore]
        public bool IsError
        {
            get
            {
                return _extraStuff.ContainsKey("error");
            }
        }

        /// <summary>
        /// Create a blank Json RPC payload
        /// </summary>
        public JsonRpcPayload()
        {
        }
    }
}
