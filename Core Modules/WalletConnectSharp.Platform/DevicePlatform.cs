using WalletConnectSharp.Network.Interfaces;

namespace WalletConnectSharp.Platform;

public static class DevicePlatform
{
    private static IPlatform? _backend;

    public static IPlatform Backend
    {
        get
        {
            return _backend ?? throw new InvalidOperationException("No backend platform");
        }
        set
        {
            _backend = value ?? throw new InvalidOperationException("Backend platform must be non-null");
        }
    }
    
    public static IConnectionBuilder ConnectionBuilder => Backend.ConnectionBuilder;
    public static Task OpenUrl(string url)
    {
        return Backend.OpenUrl(url);
    }
}
