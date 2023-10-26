using WalletConnectSharp.Core.Interfaces;

namespace WalletConnectSharp.Core.Controllers
{
    /// <summary>
    /// The HeartBeat module emits a pulse event at a specific interval simulating
    /// a heartbeat. It can be used as an setInterval replacement
    /// </summary>
    public class HeartBeat : IHeartBeat
    {
        /// <summary>
        /// The CancellationToken that stops the Heartbeat module
        /// </summary>
        public CancellationToken HeartBeatCancellationToken { get; private set; }

        public event EventHandler OnPulse;

        /// <summary>
        /// The interval (in milliseconds) the Pulse event gets emitted/triggered
        /// </summary>
        public int Interval { get; }
        
        /// <summary>
        /// The context UUID that this heartboeat module uses
        /// </summary>
        public readonly Guid contextGuid = Guid.NewGuid();

        /// <summary>
        /// The name of this Heartbeat module
        /// </summary>
        public string Name
        {
            get
            {
                return $"heartbeat-{contextGuid}";
            }
        }

        /// <summary>
        /// The context string of this Heartbeat module
        /// </summary>
        public string Context
        {
            get
            {
                return Name;
            }
        }

        /// <summary>
        /// Create a new Heartbeat module, optionally specifying options
        /// </summary>
        /// <param name="interval">The interval to emit the <see cref="IHeartBeat.OnPulse"/> event at</param>
        public HeartBeat(int interval = 5000)
        {
            Interval = interval;
        }

        /// <summary>
        /// Initialize the heartbeat module. This will start the pulse event and
        /// will continuously emit the pulse event at the configured interval. If the
        /// HeartBeatCancellationToken is cancelled, then the interval will be halted.
        /// </summary>
        /// <returns></returns>
        public Task Init()
        {
            HeartBeatCancellationToken = new CancellationToken();

            Task.Run(async () =>
            {
                while (!HeartBeatCancellationToken.IsCancellationRequested)
                {
                    Pulse();

                    await Task.Delay(Interval, HeartBeatCancellationToken);
                }
            }, HeartBeatCancellationToken);

            return Task.CompletedTask;
        }

        private void Pulse()
        {
            this.OnPulse?.Invoke(this, EventArgs.Empty);
        }

        public void Dispose()
        {
        }
    }
}
