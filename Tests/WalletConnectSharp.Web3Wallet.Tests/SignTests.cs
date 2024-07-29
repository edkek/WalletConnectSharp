using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3.Accounts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using WalletConnectSharp.Auth.Internals;
using WalletConnectSharp.Auth.Models;
using WalletConnectSharp.Common.Events;
using WalletConnectSharp.Common.Model.Errors;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Core;
using WalletConnectSharp.Core.Models;
using WalletConnectSharp.Core.Models.Verify;
using WalletConnectSharp.Network.Models;
using WalletConnectSharp.Sign;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Sign.Models.Engine;
using WalletConnectSharp.Sign.Models.Engine.Events;
using WalletConnectSharp.Sign.Models.Engine.Methods;
using WalletConnectSharp.Storage;
using WalletConnectSharp.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace WalletConnectSharp.Web3Wallet.Tests
{
    public class SignClientTests : IClassFixture<CryptoWalletFixture>, IAsyncLifetime
    {
        [RpcMethod("eth_signTransaction"), 
         RpcRequestOptions(Clock.ONE_MINUTE, 99997), 
         RpcResponseOptions(Clock.ONE_MINUTE, 99996)
        ]
        public class EthSignTransaction : List<TransactionInput>
        {
        }

        public class TestDataObject
        {
            [JsonProperty("hello")]
            public string Hello;
        }
        
        private static readonly string TestEthereumAddress = "0x3c582121909DE92Dc89A36898633C1aE4790382b";
        private static readonly string TestEthereumChain = "eip155:1";
        private static readonly string TestArbitrumChain = "eip155:42161";
        private static readonly string TestAvalancheChain = "eip155:43114";

        private static readonly string[] TestAccounts = new[]
        {
            $"{TestEthereumChain}:{TestEthereumAddress}", $"{TestArbitrumChain}:{TestEthereumAddress}",
            $"{TestAvalancheChain}:{TestEthereumAddress}"
        };

        private static readonly string[] TestEvents = new[] { "chainChanged", "accountsChanged", "valueTypeEvent", "referenceTypeEvent" };
        
        private static readonly RequestParams DefaultRequestParams = new RequestParams()
        {
            Aud = "http://localhost:3000/login",
            Domain = "localhost:3000",
            ChainId = "eip155:1",
            Nonce = CryptoUtils.GenerateNonce()
        };

        private static readonly RequiredNamespaces TestRequiredNamespaces = new RequiredNamespaces()
        {
            {
                "eip155", new ProposedNamespace()
                    {
                        Chains = new []{ "eip155:1" },
                        Methods = new[] { "eth_signTransaction" },
                        Events = TestEvents
                    }
            }
        };
        
        private static readonly Namespaces TestUpdatedNamespaces = new Namespaces()
        {
            {
                "eip155", new Namespace()
                    {
                        Methods = new []
                        {
                            "eth_signTransaction",
                            "eth_sendTransaction",
                            "personal_sign",
                            "eth_signTypedData"
                        },
                        Accounts = TestAccounts,
                        Events = TestEvents,
                        Chains = new[] { TestEthereumChain },
                    }
            }
        };

        private static readonly Namespace TestNamespace = new()
        {
            Methods = ["eth_signTransaction"], Accounts = [TestAccounts[0]], Events = TestEvents, Chains = [TestEthereumChain]
        };

        private static readonly Namespaces TestNamespaces = new()
        {
            {
                "eip155", TestNamespace
            }
        };

        private static readonly ConnectOptions TestConnectOptions = new ConnectOptions()
            .UseRequireNamespaces(TestRequiredNamespaces);
        
        private readonly CryptoWalletFixture _cryptoWalletFixture;
        private readonly ITestOutputHelper _testOutputHelper;
        private WalletConnectCore _core;
        private WalletConnectSignClient _dapp;
        private Web3WalletClient _wallet;
        private string uriString;
        private Task<SessionStruct> sessionApproval;
        private SessionStruct session;
        
        
        public string WalletAddress
        {
            get
            {
                return _cryptoWalletFixture.WalletAddress;
            }
        }

        public string Iss
        {
            get
            {
                return _cryptoWalletFixture.Iss;
            }
        }

        private static readonly string[] second = new[] { "chainChanged2" };

        public SignClientTests(CryptoWalletFixture cryptoWalletFixture, ITestOutputHelper testOutputHelper)
        {
            this._cryptoWalletFixture = cryptoWalletFixture;
            _testOutputHelper = testOutputHelper;
        }

        public async Task InitializeAsync()
        {
            _core = new WalletConnectCore(new CoreOptions()
            {
                ProjectId = TestValues.TestProjectId, RelayUrl = TestValues.TestRelayUrl,
                Name = $"wallet-csharp-test-{Guid.NewGuid().ToString()}",
                Storage = new InMemoryStorage(),
            });
            _dapp = await WalletConnectSignClient.Init(new SignClientOptions()
            {
                ProjectId = TestValues.TestProjectId,
                Name = $"dapp-csharp-test-{Guid.NewGuid().ToString()}",
                RelayUrl = TestValues.TestRelayUrl,
                Metadata = new Metadata()
                {
                    Description = "An example dapp to showcase WalletConnectSharpv2",
                    Icons = new[] { "https://walletconnect.com/meta/favicon.ico" },
                    Name = $"dapp-csharp-test-{Guid.NewGuid().ToString()}",
                    Url = "https://walletconnect.com"
                },
                Storage = new InMemoryStorage(),
            });
            var connectData = await _dapp.Connect(TestConnectOptions);
            uriString = connectData.Uri ?? "";
            sessionApproval = connectData.Approval;
            
            _wallet = await Web3WalletClient.Init(_core, new Metadata()
            {
                Description = "An example wallet to showcase WalletConnectSharpv2",
                Icons = new[] { "https://walletconnect.com/meta/favicon.ico" },
                Name = $"wallet-csharp-test-{Guid.NewGuid().ToString()}",
                Url = "https://walletconnect.com",
            }, $"wallet-csharp-test-{Guid.NewGuid().ToString()}");
            
            Assert.NotNull(_wallet);
            Assert.NotNull(_dapp);
            Assert.NotNull(_core);
            Assert.Null(_wallet.Metadata.Redirect);
        }

        public async Task DisposeAsync()
        {
            if (_core.Relayer.Connected)
            {
                await _core.Relayer.TransportClose();
            }
        }

        [Fact, Trait("Category", "unit")]
        public async Task TestShouldApproveSessionProposal()
        {
            TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();
            _wallet.SessionProposed += async (sender, @event) =>
            {
                var id = @event.Id;
                var proposal = @event.Proposal;
                var verifyContext = @event.VerifiedContext;

                Assert.Equal(Validation.Unknown, verifyContext.Validation);
                session = await _wallet.ApproveSession(id, TestNamespaces);

                Assert.Equal(proposal.RequiredNamespaces, TestRequiredNamespaces);
                task1.TrySetResult(true);
            };

            await Task.WhenAll(
                task1.Task,
                sessionApproval,
                _wallet.Pair(uriString)
            );
        }
        
        [Fact, Trait("Category", "unit")]
        public async Task TestShouldRejectSessionProposal()
        {
            var rejectionError = Error.FromErrorType(ErrorType.USER_DISCONNECTED);

            TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();
            _wallet.SessionProposed += async (sender, @event) =>
            {
                var proposal = @event.Proposal;

                var id = @event.Id;
                Assert.Equal(TestRequiredNamespaces, proposal.RequiredNamespaces);

                await _wallet.RejectSession(id, rejectionError);
                task1.TrySetResult(true);
            };

            async Task CheckSessionReject()
            {
                try
                {
                    await sessionApproval;
                }
                catch (WalletConnectException e)
                {
                    Assert.Equal(rejectionError.Code, e.Code);
                    Assert.Equal(rejectionError.Message, e.Message);
                    return;
                }
                Assert.Fail("Session approval task did not throw exception, expected rejection");
            }
            
            await Task.WhenAll(
                task1.Task,
                _wallet.Pair(uriString),
                CheckSessionReject()
            );
        }

        [Fact, Trait("Category", "unit")]
        public async Task TestUpdateSession()
        {
            TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();
            _wallet.SessionProposed += async (sender, @event) =>
            {
                var id = @event.Id;
                var proposal = @event.Proposal;
                var verifyContext = @event.VerifiedContext;

                Assert.Equal(Validation.Unknown, verifyContext.Validation);
                session = await _wallet.ApproveSession(id, TestNamespaces);

                Assert.Equal(proposal.RequiredNamespaces, TestRequiredNamespaces);
                task1.TrySetResult(true);
            };

            await Task.WhenAll(
                task1.Task,
                sessionApproval,
                _wallet.Pair(uriString)
            );
            
            Assert.NotEqual(TestNamespaces, TestUpdatedNamespaces);

            TaskCompletionSource<bool> task2 = new TaskCompletionSource<bool>();
            _dapp.SessionUpdateRequest += (sender, @event) =>
            {
                var param = @event.Params;
                Assert.Equal(TestUpdatedNamespaces, param.Namespaces);
                task2.TrySetResult(true);
            };

            await Task.WhenAll(
                task2.Task,
                _wallet.UpdateSession(session.Topic, TestUpdatedNamespaces)
            );
        }

        [Fact, Trait("Category", "unit")]
        public async Task TestExtendSession()
        {
            TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();
            _wallet.SessionProposed += async (sender, @event) =>
            {
                var id = @event.Id;
                var proposal = @event.Proposal;
                var verifyContext = @event.VerifiedContext;

                Assert.Equal(Validation.Unknown, verifyContext.Validation);
                session = await _wallet.ApproveSession(id, TestNamespaces);

                Assert.Equal(proposal.RequiredNamespaces, TestRequiredNamespaces);
                task1.TrySetResult(true);
            };

            await Task.WhenAll(
                task1.Task,
                sessionApproval,
                _wallet.Pair(uriString)
            );

            var prevExpiry = session.Expiry;
            var topic = session.Topic;
            
            // TODO Figure out if we need fake timers?
            await Task.Delay(5000);
            
            await _wallet.ExtendSession(topic);

            var updatedExpiry = _wallet.Engine.SignClient.Session.Get(topic).Expiry;
            
            Assert.True(updatedExpiry > prevExpiry);
        }

        [Fact, Trait("Category", "unit")]
        public async Task TestRespondToSessionRequest()
        {
            TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();
            _wallet.SessionProposed += async (sender, @event) =>
            {
                var id = @event.Id;
                var proposal = @event.Proposal;
                var verifyContext = @event.VerifiedContext;
                
                session = await _wallet.ApproveSession(id, new Namespaces()
                {
                    { 
                        "eip155", new Namespace()
                        {
                            Methods = TestNamespace.Methods,
                            Events = TestNamespace.Events,
                            Accounts = new []{ $"{TestEthereumChain}:{WalletAddress}" },
                            Chains = new[] { TestEthereumChain },
                        }
                    }
                });

                Assert.Equal(proposal.RequiredNamespaces, TestRequiredNamespaces);
                task1.TrySetResult(true);
            };

            await Task.WhenAll(
                task1.Task,
                sessionApproval,
                _wallet.Pair(uriString)
            );

            TaskCompletionSource<bool> task2 = new TaskCompletionSource<bool>();
            _wallet.Engine.SignClient.Engine.SessionRequestEvents<EthSignTransaction, string>()
                .OnRequest += args =>
            {
                var id = args.Request.Id;
                var @params = args.Request;
                var verifyContext = args.VerifiedContext;
                var signTransaction = @params.Params[0];
                
                Assert.Equal(Validation.Unknown, verifyContext.Validation);

                var signature = ((AccountSignerTransactionManager)_cryptoWalletFixture.CryptoWallet.GetAccount(0).TransactionManager)
                    .SignTransaction(signTransaction);

                args.Response = signature;
                task2.TrySetResult(true);

                return Task.CompletedTask;
            };

            async Task SendRequest()
            {
                var result = await _dapp.Request<EthSignTransaction, string>(session.Topic,
                    new EthSignTransaction()
                    {
                        new()
                        {
                            From = WalletAddress,
                            To = WalletAddress,
                            Data = "0x",
                            Nonce = new HexBigInteger("0x1"),
                            GasPrice = new HexBigInteger("0x020a7ac094"),
                            Gas = new HexBigInteger("0x5208"),
                            Value = new HexBigInteger("0x00")
                        }
                    }, TestEthereumChain);
                
                Assert.False(string.IsNullOrWhiteSpace(result));
            }

            await Task.WhenAll(
                task2.Task,
                SendRequest()
            );
        }

        [Fact, Trait("Category", "unit")]
        public async Task TestWalletDisconnectFromSession()
        {
            TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();
            _wallet.SessionProposed += async (sender, @event) =>
            {
                var id = @event.Id;
                var proposal = @event.Proposal;
                var verifyContext = @event.VerifiedContext;
                
                session = await _wallet.ApproveSession(id, new Namespaces()
                {
                    { 
                        "eip155", new Namespace()
                        {
                            Methods = TestNamespace.Methods,
                            Events = TestNamespace.Events,
                            Accounts = new []{ $"{TestEthereumChain}:{WalletAddress}" },
                            Chains = new [] { TestEthereumChain }
                        }
                    }
                });

                Assert.Equal(proposal.RequiredNamespaces, TestRequiredNamespaces);
                task1.TrySetResult(true);
            };

            await Task.WhenAll(
                task1.Task,
                sessionApproval,
                _wallet.Pair(uriString)
            );

            var reason = Error.FromErrorType(ErrorType.USER_DISCONNECTED);
            
            TaskCompletionSource<bool> task2 = new TaskCompletionSource<bool>();
            _dapp.SessionDeleted += (sender, @event) =>
            {
                Assert.Equal(session.Topic, @event.Topic);
                task2.TrySetResult(true);
            };

            await Task.WhenAll(
                task2.Task,
                _wallet.DisconnectSession(session.Topic, reason)
            );
        }
        
        [Fact, Trait("Category", "unit")]
        public async Task TestDappDisconnectFromSession()
        {
            TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();
            _wallet.SessionProposed += async (sender, @event) =>
            {
                var id = @event.Id;
                var proposal = @event.Proposal;
                var verifyContext = @event.VerifiedContext;
                
                session = await _wallet.ApproveSession(id, new Namespaces()
                {
                    { 
                        "eip155", new Namespace()
                        {
                            Methods = TestNamespace.Methods,
                            Events = TestNamespace.Events,
                            Accounts = new []{ $"{TestEthereumChain}:{WalletAddress}" },
                            Chains = new [] { TestEthereumChain }
                        }
                    }
                });

                Assert.Equal(proposal.RequiredNamespaces, TestRequiredNamespaces);
                task1.TrySetResult(true);
            };

            await Task.WhenAll(
                task1.Task,
                sessionApproval,
                _wallet.Pair(uriString)
            );

            var reason = Error.FromErrorType(ErrorType.USER_DISCONNECTED);

            TaskCompletionSource<bool> task2 = new TaskCompletionSource<bool>();
            _wallet.SessionDeleted += (sender, @event) =>
            {
                Assert.Equal(session.Topic, @event.Topic);
                task2.TrySetResult(true);
            };

            await Task.WhenAll(
                task2.Task,
                _dapp.Disconnect(session.Topic, reason)
            );
        }

        [Fact, Trait("Category", "unit")]
        public async Task TestEmitSessionEvent()
        {
            var pairingTask = new TaskCompletionSource<bool>();
            _wallet.SessionProposed += async (sender, @event) =>
            {
                var id = @event.Id;
                var proposal = @event.Proposal;
                
                session = await _wallet.ApproveSession(id, new Namespaces()
                {
                    { 
                        "eip155", new Namespace()
                        {
                            Methods = TestNamespace.Methods,
                            Events = TestNamespace.Events,
                            Accounts = [$"{TestEthereumChain}:{WalletAddress}"],
                            Chains = [TestEthereumChain]
                        }
                    }
                });

                Assert.Equal(proposal.RequiredNamespaces, TestRequiredNamespaces);
                pairingTask.TrySetResult(true);
            };

            await Task.WhenAll(
                pairingTask.Task,
                sessionApproval,
                _wallet.Pair(uriString)
            );

            var referenceHandlingTask = new TaskCompletionSource<bool>();
            var valueHandlingTask = new TaskCompletionSource<bool>();

            var referenceTypeEventData = new EventData<TestDataObject>
            {
                Name = "referenceTypeEvent",
                Data = new TestDataObject
                {
                    Hello = "World"
                }
            };

            var valueTypeEventData = new EventData<long> { Name = "valueTypeEvent", Data = 10 };

            void ReferenceTypeEventHandler(object _, SessionEvent<JToken> data)
            {
                var eventData = data.Event.Data.ToObject<TestDataObject>();

                Assert.Equal(referenceTypeEventData.Name, data.Event.Name);
                Assert.Equal(referenceTypeEventData.Data.Hello, eventData.Hello);

                referenceHandlingTask.TrySetResult(true);
            }

            void ValueTypeEventHandler(object _, SessionEvent<JToken> eventData)
            {
                var data = eventData.Event.Data.Value<long>();

                Assert.Equal(valueTypeEventData.Name, eventData.Event.Name);
                Assert.Equal(valueTypeEventData.Data, data);

                valueHandlingTask.TrySetResult(true);
            }

            _dapp.SubscribeToSessionEvent(referenceTypeEventData.Name, ReferenceTypeEventHandler);
            _dapp.SubscribeToSessionEvent(valueTypeEventData.Name, ValueTypeEventHandler);

            await Task.WhenAll(
                referenceHandlingTask.Task,
                valueHandlingTask.Task,
                _wallet.EmitSessionEvent(session.Topic, referenceTypeEventData, TestRequiredNamespaces["eip155"].Chains[0]),
                _wallet.EmitSessionEvent(session.Topic, valueTypeEventData, TestRequiredNamespaces["eip155"].Chains[0])
            );

            Assert.True(_dapp.TryUnsubscribeFromSessionEvent(referenceTypeEventData.Name, ReferenceTypeEventHandler));
            Assert.True(_dapp.TryUnsubscribeFromSessionEvent(valueTypeEventData.Name, ValueTypeEventHandler));

            // Test invalid chains
            await Assert.ThrowsAsync<FormatException>(() => _wallet.EmitSessionEvent(session.Topic, valueTypeEventData, "invalid chain"));
            await Assert.ThrowsAsync<NamespacesException>(() => _wallet.EmitSessionEvent(session.Topic, valueTypeEventData, "123:321"));
        }

        [Fact, Trait("Category", "unit")]
        public async Task TestGetActiveSessions()
        {
            TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();
            _wallet.SessionProposed += async (sender, @event) =>
            {
                var id = @event.Id;
                var proposal = @event.Proposal;
                var verifyContext = @event.VerifiedContext;

                session = await _wallet.ApproveSession(id,
                    new Namespaces()
                    {
                        {
                            "eip155",
                            new Namespace()
                            {
                                Methods = TestNamespace.Methods,
                                Events = TestNamespace.Events,
                                Accounts = new[] { $"{TestEthereumChain}:{WalletAddress}" },
                                Chains = new [] { TestEthereumChain }
                            }
                        }
                    });

                Assert.Equal(proposal.RequiredNamespaces, TestRequiredNamespaces);
                task1.TrySetResult(true);
            };

            await Task.WhenAll(
                task1.Task,
                sessionApproval,
                _wallet.Pair(uriString)
            );

            var sessions = _wallet.ActiveSessions;
            Assert.NotNull(sessions);
            Assert.Single(sessions);
            Assert.Equal(session.Topic, sessions.Values.ToArray()[0].Topic);
        }

        [Fact, Trait("Category", "unit")]
        public async Task TestGetPendingSessionProposals()
        {
            TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();
            _wallet.SessionProposed += (sender, @event) =>
            {
                var proposals = _wallet.PendingSessionProposals;
                Assert.NotNull(proposals);
                Assert.Single(proposals);
                Assert.Equal(TestRequiredNamespaces, proposals.Values.ToArray()[0].RequiredNamespaces);
                task1.TrySetResult(true);
            };

            await Task.WhenAll(
                task1.Task,
                _wallet.Pair(uriString)
            );
        }

        [Fact, Trait("Category", "unit")]
        public async Task TestGetPendingSessionRequests()
        {
            TaskCompletionSource<bool> task1 = new TaskCompletionSource<bool>();
            _wallet.SessionProposed += async (sender, @event) =>
            {
                var id = @event.Id;
                var proposal = @event.Proposal;
                var verifyContext = @event.VerifiedContext;

                session = await _wallet.ApproveSession(id,
                    new Namespaces()
                    {
                        {
                            "eip155",
                            new Namespace()
                            {
                                Methods = TestNamespace.Methods,
                                Events = TestNamespace.Events,
                                Accounts = new[] { $"{TestEthereumChain}:{WalletAddress}" },
                                Chains = new [] { TestEthereumChain }
                            }
                        }
                    });

                Assert.Equal(proposal.RequiredNamespaces, TestRequiredNamespaces);
                task1.TrySetResult(true);
            };

            await Task.WhenAll(
                task1.Task,
                sessionApproval,
                _wallet.Pair(uriString)
            );

            var requestParams = new EthSignTransaction()
            {
                new()
                {
                    From = WalletAddress,
                    To = WalletAddress,
                    Data = "0x",
                    Nonce = new HexBigInteger("0x1"),
                    GasPrice = new HexBigInteger("0x020a7ac094"),
                    Gas = new HexBigInteger("0x5208"),
                    Value = new HexBigInteger("0x00")
                }
            };
            
            TaskCompletionSource<bool> task2 = new TaskCompletionSource<bool>();
            _wallet.Engine.SignClient.Engine.SessionRequestEvents<EthSignTransaction, string>()
                .OnRequest += args =>
            {
                // Get the pending session request, since that's what we're testing
                var pendingRequests = _wallet.PendingSessionRequests;
                var request = pendingRequests[0];
                
                var id = request.Id;
                var verifyContext = args.VerifiedContext;
                
                // Perform unsafe cast, all pending requests are stored as object type
                var signTransaction = ((EthSignTransaction)request.Parameters.Request.Params)[0];

                Assert.Equal(args.Request.Id, id);
                Assert.Equal(Validation.Unknown, verifyContext.Validation);

                var signature = ((AccountSignerTransactionManager)_cryptoWalletFixture.CryptoWallet.GetAccount(0).TransactionManager)
                    .SignTransaction(signTransaction);

                args.Response = signature;
                task2.TrySetResult(true);
                return Task.CompletedTask;
            };

            async Task SendRequest()
            {
                var result = await _dapp.Request<EthSignTransaction, string>(session.Topic, 
                    requestParams, TestEthereumChain);
                
                Assert.False(string.IsNullOrWhiteSpace(result));
            }

            await Task.WhenAll(
                task2.Task,
                SendRequest()
            );
        }
    }
}
