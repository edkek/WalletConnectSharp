namespace WalletConnectSharp.Tests.Common
{
    public static class TestValues
    {
        private const string DefaultProjectId = "ef21cf313a63dbf63f2e9e04f3614029";
        private static readonly string EnvironmentProjectId = Environment.GetEnvironmentVariable("PROJECT_ID");

        public static readonly string TestProjectId = !string.IsNullOrWhiteSpace(EnvironmentProjectId)
            ? EnvironmentProjectId
            : DefaultProjectId;

        private const string DefaultRelayUrl = "wss://relay.walletconnect.org";

        private static readonly string EnvironmentRelayUrl = Environment.GetEnvironmentVariable("RELAY_ENDPOINT");

        public static readonly string TestRelayUrl =
            !string.IsNullOrWhiteSpace(EnvironmentRelayUrl) ? EnvironmentRelayUrl : DefaultRelayUrl;

        private static readonly string EnvironmentClientCount = Environment.GetEnvironmentVariable("CLIENTS");

        public static readonly int ClientCount = !string.IsNullOrWhiteSpace(EnvironmentClientCount)
            ? int.Parse(EnvironmentClientCount)
            : 200;

        private static readonly string EnvironmentMessageCount =
            Environment.GetEnvironmentVariable("MESSAGES_PER_CLIENT");

        public static readonly int MessagesPerClient = !string.IsNullOrWhiteSpace(EnvironmentMessageCount)
            ? int.Parse(EnvironmentMessageCount)
            : 1000;

        private static readonly string EnvironmentHeartbeatInterval =
            Environment.GetEnvironmentVariable("HEARTBEAT_INTERVAL");

        public static readonly int HeartbeatInterval = !string.IsNullOrWhiteSpace(EnvironmentHeartbeatInterval)
            ? int.Parse(EnvironmentHeartbeatInterval)
            : 3000;
    }
}
