using WalletConnectSharp.Common.Model.Errors;
using WalletConnectSharp.Common.Model.Relay;
using WalletConnectSharp.Network.Models;
using WalletConnectSharp.Network.Tests.Models;
using WalletConnectSharp.Network.Websocket;
using WalletConnectSharp.Tests.Common;
using Xunit;

namespace WalletConnectSharp.Network.Tests
{
    public class RelayTests
    {
        private static readonly JsonRpcRequest<TopicData> TEST_IRN_REQUEST =
            new JsonRpcRequest<TopicData>(RelayProtocols.DefaultProtocol.Subscribe,
                new TopicData() { Topic = "ca838d59a3a3fe3824dab9ca7882ac9a2227c5d0284c88655b261a2fe85db270" });

        private static readonly JsonRpcRequest<TopicData> TEST_BAD_IRN_REQUEST =
            new JsonRpcRequest<TopicData>(RelayProtocols.DefaultProtocol.Subscribe, new TopicData());

        private static readonly string DEFAULT_GOOD_WS_URL = "wss://relay.walletconnect.org";

        private static readonly string ENVIRONMENT_DEFAULT_GOOD_WS_URL =
            Environment.GetEnvironmentVariable("RELAY_ENDPOINT");

        private static readonly string GOOD_WS_URL = !string.IsNullOrWhiteSpace(ENVIRONMENT_DEFAULT_GOOD_WS_URL)
            ? ENVIRONMENT_DEFAULT_GOOD_WS_URL
            : DEFAULT_GOOD_WS_URL;

        private static readonly string TEST_RANDOM_HOST = "random.domain.that.does.not.exist";
        private static readonly string BAD_WS_URL = "ws://" + TEST_RANDOM_HOST;

        public async Task<string> BuildGoodURL()
        {
            var crypto = new Crypto.Crypto();
            await crypto.Init();

            var auth = await crypto.SignJwt(GOOD_WS_URL);

            var relayUrlBuilder = new RelayUrlBuilder();
            return relayUrlBuilder.FormatRelayRpcUrl(
                GOOD_WS_URL,
                RelayProtocols.Default,
                RelayConstants.Version.ToString(),
                TestValues.TestProjectId,
                auth
            );
        }

        [Fact, Trait("Category", "integration")]
        public async Task ConnectAndRequest()
        {
            var url = await BuildGoodURL();
            var connection = new WebsocketConnection(url);
            var provider = new JsonRpcProvider(connection);
            await provider.Connect();

            var result = await provider.Request<TopicData, string>(TEST_IRN_REQUEST);

            Assert.True(result.Length > 0);
        }

        [Fact, Trait("Category", "integration")]
        public async Task RequestWithoutConnect()
        {
            var url = await BuildGoodURL();
            var connection = new WebsocketConnection(url);
            var provider = new JsonRpcProvider(connection);

            var result = await provider.Request<TopicData, string>(TEST_IRN_REQUEST);

            Assert.True(result.Length > 0);
        }

        [Fact, Trait("Category", "integration")]
        public async Task ThrowOnJsonRpcError()
        {
            var url = await BuildGoodURL();
            var connection = new WebsocketConnection(url);
            var provider = new JsonRpcProvider(connection);

            await Assert.ThrowsAsync<WalletConnectException>(() =>
                provider.Request<TopicData, string>(TEST_BAD_IRN_REQUEST));
        }

        [Fact, Trait("Category", "integration")]
        public async Task ThrowsOnUnavailableHost()
        {
            var connection = new WebsocketConnection(BAD_WS_URL);
            var provider = new JsonRpcProvider(connection);

            await Assert.ThrowsAsync<TimeoutException>(() => provider.Request<TopicData, string>(TEST_IRN_REQUEST));
        }

        [Fact, Trait("Category", "integration")]
        public async Task ReconnectsWithNewProvidedHost()
        {
            var url = await BuildGoodURL();
            var connection = new WebsocketConnection(BAD_WS_URL);
            var provider = new JsonRpcProvider(connection);
            Assert.Equal(BAD_WS_URL, provider.Connection.Url);
            await provider.Connect(url);
            Assert.Equal(url, provider.Connection.Url);

            var result = await provider.Request<TopicData, string>(TEST_IRN_REQUEST);

            Assert.True(result.Length > 0);
        }

        [Fact, Trait("Category", "integration")]
        public async Task DoesNotDoubleRegisterListeners()
        {
            var url = await BuildGoodURL();
            var connection = new WebsocketConnection(url);
            var provider = new JsonRpcProvider(connection);

            var expectedDisconnectCount = 3;
            var disconnectCount = 0;

            provider.Disconnected += (_, _) => disconnectCount++;

            await provider.Connect();
            await provider.Disconnect();
            await provider.Connect();
            await provider.Disconnect();
            await provider.Connect();
            await provider.Disconnect();

            Assert.Equal(expectedDisconnectCount, disconnectCount);
        }
    }
}
