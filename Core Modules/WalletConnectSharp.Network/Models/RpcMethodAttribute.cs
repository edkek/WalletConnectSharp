using System;
using System.Linq;

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
        /// Get the method name to use for a given type <typeparamref name="T"/>. 
        /// If <typeparamref name="T"/> is a generic type, this method first attempts
        /// to retrieve the method name from its generic arguments.
        /// If a method name is found among the generic arguments, it is returned.
        /// Otherwise, it falls back to retrieve the method name from the type <typeparamref name="T"/> itself.
        /// If <typeparamref name="T"/> has no RpcMethodAttribute, then an Exception is thrown.
        /// </summary>
        /// <typeparam name="T">The type to get the method name for</typeparam>
        /// <returns>
        /// The method name to use, either retrieved from the generic arguments
        /// of <typeparamref name="T"/> or from <typeparamref name="T"/> itself.
        /// </returns>
        /// <exception cref="Exception">If <typeparamref name="T"/> has no RpcMethodAttribute,
        /// then an Exception is thrown</exception>
        public static string MethodForType<T>()
        {
            var genericMethod = GenericMethodForType<T>();
            if (genericMethod != null)
            {
                return genericMethod;
            }
            
            var attributes = typeof(T).GetCustomAttributes(typeof(RpcMethodAttribute), true);
            if (attributes.Length != 1)
                throw new Exception($"Type {typeof(T).FullName} has no WcMethod attribute!");
            
            var method = attributes.Cast<RpcMethodAttribute>().First().MethodName;
            return method;
        }

        /// <summary>
        /// Get the method name to use for a given generic type <typeparamref name="T"/>.
        /// If the type <typeparamref name="T"/> is not generic, returns null.
        /// This method iterates over the generic arguments of <typeparamref name="T"/>
        /// and retrieves the method name defined by the RpcMethodAttribute attached
        /// to any of its generic arguments. If none of the generic arguments
        /// of <typeparamref name="T"/> have the RpcMethodAttribute, null is returned.
        /// </summary>
        /// <typeparam name="T">The generic type to get the method name for</typeparam>
        /// <returns>
        /// The method name defined by the RpcMethodAttribute attached to one of the generic arguments
        /// of <typeparamref name="T"/>, or null if none of the generic arguments
        /// have the RpcMethodAttribute.
        /// </returns>
        private static string GenericMethodForType<T>()
        {
            if (!typeof(T).IsGenericType)
            {
                return null;
            }

            var args = typeof(T).GetGenericArguments();
            foreach (var arg in args)
            {
                var attributes = arg.GetCustomAttributes(typeof(RpcMethodAttribute), true);
                if (attributes.Length != 1)
                    continue;
            
                return attributes.Cast<RpcMethodAttribute>().First().MethodName;
            }

            return null;
        }
    }
}
