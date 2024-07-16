namespace WalletConnectSharp.Common.Model.Errors
{
    /// <summary>
    /// A helper class for generating error messages
    /// based on an ErrorType
    /// </summary>
    public static class SdkErrors
    {
        /// <summary>
        /// Generate an error message using an ErrorType code, a message parameters
        /// and a dictionary of parameters for the error message
        /// </summary>
        /// <param name="type">The error type message to generate</param>
        /// <param name="context">Additional context</param>
        /// <returns>The error message as a string</returns>
        public static string MessageFromType(ErrorType type, string context = null)
        {
            string errorMessage;
            switch (type)
            {
                default:
                case ErrorType.GENERIC:
                    errorMessage = "{message}";
                    break;
                case ErrorType.JSONRPC_REQUEST_TIMEOUT:
                    errorMessage = "JSON-RPC Request timeout after {timeout} seconds: {method}";
                    break;
                case ErrorType.UNAUTHORIZED_TARGET_CHAIN:
                    errorMessage = "Unauthorized Target ChainId Requested/";
                    break;
                case ErrorType.UNAUTHORIZED_JSON_RPC_METHOD:
                    errorMessage = "Unauthorized JSON-RPC Method Requested.";
                    break;
                case ErrorType.UNAUTHORIZED_NOTIFICATION_TYPE:
                    errorMessage = "Unauthorized Notification Type Requested.";
                    break;
                case ErrorType.UNAUTHORIZED_UPDATE_REQUEST:
                    errorMessage = "Unauthorized {context} update request";
                    break;
                case ErrorType.UNAUTHORIZED_UPGRADE_REQUEST:
                    errorMessage = "Unauthorized {context} upgrade request";
                    break;
                case ErrorType.UNAUTHORIZED_EXTEND_REQUEST:
                    errorMessage = "Unauthorized {context} extend request";
                    break;
                case ErrorType.UNAUTHORIZED_MATCHING_CONTROLLER:
                    errorMessage = "Unauthorized: method {method} not allowed";
                    break;
                case ErrorType.UNAUTHORIZED_METHOD:
                    errorMessage = "Unauthorized: peer is also {controller} controller";
                    break;
                case ErrorType.JSONRPC_REQUEST_METHOD_REJECTED:
                    errorMessage = "User rejected the request.";
                    break;
                case ErrorType.JSONRPC_REQUEST_METHOD_UNAUTHORIZED:
                    errorMessage = "The requested account and/or method has not been authorized by the user.";
                    break;
                case ErrorType.JSONRPC_REQUEST_METHOD_UNSUPPORTED:
                    errorMessage = "The requested method is not supported by this {blockchain} provider.";
                    break;
                case ErrorType.DISCONNECTED_ALL_CHAINS:
                    errorMessage = "The provider is disconnected from all chains.";
                    break;
                case ErrorType.DISCONNECTED_TARGET_CHAIN:
                    errorMessage = "The provider is disconnected from the specified chain.";
                    break;
                case ErrorType.DISAPPROVED_CHAINS:
                    errorMessage = "User disapproved requested chains";
                    break;
                case ErrorType.DISAPPROVED_JSONRPC:
                    errorMessage = "JSON-RPC disapproved request";
                    break;
                case ErrorType.DISAPPROVED_NOTIFICATION:
                    errorMessage = "User disapproved requested notification types";
                    break;
                case ErrorType.UNSUPPORTED_CHAINS:
                    errorMessage = "Requested chains are not supported: {chains}";
                    break;
                case ErrorType.UNSUPPORTED_JSONRPC:
                    errorMessage = "Requested json-rpc methods are not supported: {methods}";
                    break;
                case ErrorType.UNSUPPORTED_NOTIFICATION:
                    errorMessage = "Requested notification types are not supported: {types}";
                    break;
                case ErrorType.UNSUPPORTED_ACCOUNTS:
                    errorMessage = "{message}";
                    break;
                case ErrorType.USER_DISCONNECTED:
                    errorMessage = "User disconnected.";
                    break;
                case ErrorType.UNKNOWN:
                    errorMessage = "Unknown error {params}";
                    break;
            }

            if (context == null)
            {
                return errorMessage;
            }
            else
            {
                return $"{errorMessage} {context}";
            }
        }
    }
}
