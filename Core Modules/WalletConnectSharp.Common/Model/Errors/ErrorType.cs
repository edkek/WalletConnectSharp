namespace WalletConnectSharp.Common.Model.Errors
{
    /// <summary>
    /// The ErrorType is an enum of error codes defined by
    /// their name
    /// </summary>
    public enum ErrorType : uint
    {
        // 0 (Generic)
        GENERIC = 0,

        // 2000 (Timeout)
        SETTLE_TIMEOUT = 2000,
        JSONRPC_REQUEST_TIMEOUT = 2001,

        // 3000 (Unauthorized)
        UNAUTHORIZED_TARGET_CHAIN = 3000,
        UNAUTHORIZED_JSON_RPC_METHOD = 3001,
        UNAUTHORIZED_NOTIFICATION_TYPE = 3002,
        UNAUTHORIZED_UPDATE_REQUEST = 3003,
        UNAUTHORIZED_UPGRADE_REQUEST = 3004,
        UNAUTHORIZED_EXTEND_REQUEST = 3005,
        UNAUTHORIZED_MATCHING_CONTROLLER = 3100,
        UNAUTHORIZED_METHOD = 3101,

        // 4000 (EIP-1193)
        JSONRPC_REQUEST_METHOD_REJECTED = 4001,
        JSONRPC_REQUEST_METHOD_UNAUTHORIZED = 4100,
        JSONRPC_REQUEST_METHOD_UNSUPPORTED = 4200,
        DISCONNECTED_ALL_CHAINS = 4900,
        DISCONNECTED_TARGET_CHAIN = 4901,
        
        // 5000 (CAIP-25)
        DISAPPROVED_CHAINS = 5000,
        DISAPPROVED_JSONRPC = 5001,
        DISAPPROVED_NOTIFICATION = 5002,
        UNSUPPORTED_CHAINS = 5100,
        UNSUPPORTED_JSONRPC = 5101,
        UNSUPPORTED_NOTIFICATION = 5102,
        UNSUPPORTED_ACCOUNTS = 5103,

        // 6000 (Reason)
        USER_DISCONNECTED = 6000,

        // 8000 (Session)
        SESSION_REQUEST_EXPIRED = 8000,

        // 9000 (Unknown)
        UNKNOWN = 9000,
    }
}
