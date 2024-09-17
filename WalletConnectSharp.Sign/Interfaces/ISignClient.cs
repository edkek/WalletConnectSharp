using WalletConnectSharp.Common;
using WalletConnectSharp.Core;
using WalletConnectSharp.Core.Interfaces;
using WalletConnectSharp.Sign.Models;

namespace WalletConnectSharp.Sign.Interfaces
{
    /// <summary>
    /// An interface for the Sign Client. This includes modules the Sign Client will use, the ICore module
    /// this Sign Client is using, as well as public facing Engine functions and properties.
    /// </summary>
    [Obsolete("WalletConnectSharp is now considered deprecated and will reach End-of-Life on February 17th 2025. For more details, including migration guides please see: https://docs.reown.com")]
    public interface ISignClient : IModule, IEngineAPI
    {
        /// <summary>
        /// The Metadata this Sign Client is broadcasting with
        /// </summary>
        Metadata Metadata { get; }
        
        /// <summary>
        /// The module that holds the logic for handling the default session & chain, and for fetching the current address
        /// for any session or the default session. 
        /// </summary>
        IAddressProvider AddressProvider { get; }
        
        /// <summary>
        /// The <see cref="ICore"/> module this Sign Client is using
        /// </summary>
        ICore Core { get; }
        
        /// <summary>
        /// The <see cref="IEngine"/> module this Sign Client is using
        /// </summary>
        IEngine Engine { get; }

        /// <summary>
        /// The <see cref="ISession"/> module this Sign Client is using to store Session data
        /// </summary>
        ISession Session { get; }
        
        /// <summary>
        /// The <see cref="IProposal"/> module this Sign Client is using to store Proposal data
        /// </summary>
        IProposal Proposal { get; }
        
        IPendingRequests PendingRequests { get; }

        /// <summary>
        /// The options this Sign Client was initialized with
        /// </summary>
        SignClientOptions Options { get; }
        
        /// <summary>
        /// The protocol (represented as a string) this Sign Client is using
        /// </summary>
        string Protocol { get; }
        
        /// <summary>
        /// The version of this Sign Client implementation
        /// </summary>
        int Version { get; }
    }
}
