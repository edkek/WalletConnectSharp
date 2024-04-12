namespace WalletConnectSharp.Tests.Common;

public abstract class TwoClientsFixture<TClient> where TClient : IDisposable
{
    public TClient ClientA { get; protected set; }
    public TClient ClientB { get; protected set; }
    

    public TwoClientsFixture(bool initNow = true)
    {
        if (initNow)
            Init();
    }

    public abstract Task Init();

    public async Task WaitForClientsReady()
    {
        while (ClientA == null || ClientB == null)
            await Task.Delay(10);
    }

    public virtual async Task DisposeAndReset()
    {
        if (!Equals(ClientA, default(TClient)))
        {
            ClientA.Dispose();
            ClientA = default;
        }

        if (!Equals(ClientB, default(TClient)))
        {
            ClientB.Dispose();
            ClientB = default;
        }

        await Task.Delay(500);

        await Init();
    }
}
