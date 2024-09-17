using WalletConnectSharp.Common;
using WalletConnectSharp.Core;
using WalletConnectSharp.Core.Interfaces;

namespace WalletConnectSharp.Web3Wallet.Interfaces;

[Obsolete("WalletConnectSharp is now considered deprecated and will reach End-of-Life on February 17th 2025. For more details, including migration guides please see: https://docs.reown.com")]
public interface IWeb3Wallet : IModule, IWeb3WalletApi
{
    IWeb3WalletEngine Engine { get; }
    
    ICore Core { get; }
    
    Metadata Metadata { get; }
}
