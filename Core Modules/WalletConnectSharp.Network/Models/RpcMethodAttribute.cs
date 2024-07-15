namespace WalletConnectSharp.Network.Models
{
    /// <summary>
    /// A class (or struct) attribute that defines the Rpc method
    /// that should be used when the class is used as request parameters.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
    public class RpcMethodAttribute : Attribute
    {
        /// <summary>
        /// The Json RPC method to use when this class is used
        /// as a request parameter
        /// </summary>
        public string MethodName { get; }

        /// <summary>
        /// Define the Json RPC method to use when this class is
        /// used as a request parameter
        /// </summary>
        /// <param name="method">The method name to use</param>
        public RpcMethodAttribute(string method)
        {
            MethodName = method;
        }

        /// <summary>
        ///     Retrieves the method name to be used for a given class type T, as defined by the RpcMethodAttribute
        ///     attached to type T. This method ensures that exactly one RpcMethodAttribute is present on the type T.
        ///     If no RpcMethodAttribute is found, or if multiple are found, an InvalidOperationException is thrown.
        /// </summary>
        /// <typeparam name="T">The type T for which to get the method name.</typeparam>
        /// <returns>The method name to use, as a string.</returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the type T has no RpcMethodAttribute defined,
        ///     or if multiple RpcMethodAttribute definitions are found.
        /// </exception>
        public static string MethodForType<T>()
        {
            var attributes = typeof(T).GetCustomAttributes(typeof(RpcMethodAttribute), true);
            switch (attributes.Length)
            {
                case 0:
                    throw new InvalidOperationException($"Type {typeof(T).FullName} has no {nameof(RpcMethodAttribute)} defined.");
                case > 1:
                    throw new InvalidOperationException($"Type {typeof(T).FullName} has multiple {nameof(RpcMethodAttribute)} definitions. Only one is allowed.");
            }

            var methodAttribute = attributes.Cast<RpcMethodAttribute>().SingleOrDefault();
            if (methodAttribute == null)
            {
                throw new InvalidOperationException($"Type {typeof(T).FullName} has multiple RpcMethodAttribute definitions. Only one is allowed.");
            }

            return methodAttribute.MethodName;
        }
    }
}
