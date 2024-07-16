using WalletConnectSharp.Common.Model.Errors;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Network.Models;
using WalletConnectSharp.Sign.Interfaces;
using WalletConnectSharp.Sign.Models;
using WalletConnectSharp.Sign.Models.Engine;
using WalletConnectSharp.Tests.Common;
using Xunit;
using Xunit.Abstractions;

namespace WalletConnectSharp.Sign.Test
{
    public class SignTests : IClassFixture<SignClientFixture>
    {
        private SignClientFixture _cryptoFixture;
        private readonly ITestOutputHelper _testOutputHelper;
        private const string AllowedChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";

        [RpcMethod("test_method"), RpcRequestOptions(Clock.ONE_MINUTE, 99998)]
        public class TestRequest
        {
            public int a;
            public int b;
        }
        
        [RpcMethod("test_method_2"), 
         RpcRequestOptions(Clock.ONE_MINUTE, 99997), 
         RpcResponseOptions(Clock.ONE_MINUTE, 99996)]
        public class TestRequest2
        {
            public string x;
            public int y;
        }

        // represents array of strings requests, similar to personal_sign
        [RpcMethod("complex_test_method")] [RpcRequestOptions(Clock.ONE_MINUTE, 99990)]
        public class ComplexTestRequest : List<string>
        {
            public ComplexTestRequest()
            {
            }

            public ComplexTestRequest(params string[] args) : base(args)
            {
            }

            public int A
            {
                get
                {
                    if (Count != 2 || !int.TryParse(this[0], out var a))
                    {
                        return 0;
                    }

                    return a;
                }
            }

            public int B
            {
                get
                {
                    if (Count != 2 || !int.TryParse(this[1], out var b))
                    {
                        return 0;
                    }

                    return b;
                }
            }
        }

        // represents array of objects requests, similar to eth_sendTransaction
        [RpcMethod("complex_test_method_2")] [RpcRequestOptions(Clock.ONE_MINUTE, 99991)] [RpcResponseOptions(Clock.ONE_MINUTE, 99992)]
        public class ComplexTestRequest2 : List<TestRequest2>
        {
            public ComplexTestRequest2()
            {
            }

            public ComplexTestRequest2(params TestRequest2[] args) : base(args)
            {
            }

            public string X => this.FirstOrDefault()?.x ?? string.Empty;

            public int Y => this.FirstOrDefault()?.y ?? -1;
        }

        [RpcResponseOptions(Clock.ONE_MINUTE, 99999)]
        public class TestResponse
        {
            public int result;
        }

        public WalletConnectSignClient ClientA
        {
            get
            {
                return _cryptoFixture.ClientA;
            }
        }
        
        public WalletConnectSignClient ClientB
        {
            get
            {
                return _cryptoFixture.ClientB;
            }
        }

        public SignTests(SignClientFixture cryptoFixture, ITestOutputHelper testOutputHelper)
        {
            this._cryptoFixture = cryptoFixture;
            _testOutputHelper = testOutputHelper;
        }

        public static async Task<SessionStruct> TestConnectMethod(ISignClient clientA, ISignClient clientB)
        {
            var start = Clock.NowMilliseconds();
            
            var testAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
            var dappConnectOptions = new ConnectOptions()
            {
                RequiredNamespaces = new RequiredNamespaces()
                {
                    {
                        "eip155", new ProposedNamespace()
                        {
                            Methods = new[]
                            {
                                "eth_sendTransaction",
                                "eth_signTransaction",
                                "eth_sign",
                                "personal_sign",
                                "eth_signTypedData",
                            },
                            Chains = new[]
                            {
                                "eip155:1", "eip155:10"
                            },
                            Events = new[]
                            {
                                "chainChanged", "accountsChanged"
                            }
                        }
                    }
                }
            };

            var dappClient = clientA;
            var connectData = await dappClient.Connect(dappConnectOptions);

            var walletClient = clientB;
            var proposal = await walletClient.Pair(connectData.Uri);
            
            Assert.NotNull(proposal.RequiredNamespaces);
            Assert.NotNull(proposal.OptionalNamespaces);
            Assert.True(proposal.SessionProperties == null || proposal.SessionProperties.Count > 0);
            Assert.NotNull(proposal.Expiry);
            Assert.NotNull(proposal.Relays);
            Assert.NotNull(proposal.Proposer);
            Assert.NotNull(proposal.PairingTopic);

            var approveData = await walletClient.Approve(proposal, testAddress);

            var sessionData = await connectData.Approval;
            await approveData.Acknowledged();

            Assert.True(clientA.Find(dappConnectOptions.RequiredNamespaces).Length != 0);
            Assert.True(clientA.Find(new RequiredNamespaces()).Length == 0);
            
            return sessionData;
        }

        [Fact, Trait("Category", "integration")]
        public async Task TestApproveSession()
        {
            await _cryptoFixture.WaitForClientsReady();

            await TestConnectMethod(ClientA, ClientB);
        }
        
        [Fact, Trait("Category", "integration")]
        public async Task TestRejectSession()
        {
            await _cryptoFixture.WaitForClientsReady();
            
            var testAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
            var dappConnectOptions = new ConnectOptions()
            {
                RequiredNamespaces = new RequiredNamespaces()
                {
                    {
                        "eip155", new ProposedNamespace()
                        {
                            Methods = new[]
                            {
                                "eth_sendTransaction",
                                "eth_signTransaction",
                                "eth_sign",
                                "personal_sign",
                                "eth_signTypedData",
                            },
                            Chains = new[]
                            {
                                "eip155:1", "eip155:10"
                            },
                            Events = new[]
                            {
                                "chainChanged", "accountsChanged"
                            }
                        }
                    }
                }
            };

            var dappClient = ClientA;
            var connectData = await dappClient.Connect(dappConnectOptions);

            var walletClient = ClientB;
            var proposal = await walletClient.Pair(connectData.Uri);

            await walletClient.Reject(proposal);

            await Assert.ThrowsAsync<WalletConnectException>(() => connectData.Approval);
        }
        
        [Fact, Trait("Category", "integration")]
        public async Task TestSessionRequestResponse()
        {
            await _cryptoFixture.WaitForClientsReady();
            
            var testAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
            var testMethod = "test_method";
            
            var dappConnectOptions = new ConnectOptions()
            {
                RequiredNamespaces = new RequiredNamespaces()
                {
                    {
                        "eip155", new ProposedNamespace()
                        {
                            Methods = new[]
                            {
                                testMethod
                            },
                            Chains = new[]
                            {
                                "eip155:1", "eip155:10"
                            },
                            Events = new[]
                            {
                                "chainChanged", "accountsChanged"
                            }
                        }
                    }
                }
            };

            var dappClient = ClientA;
            var connectData = await dappClient.Connect(dappConnectOptions);

            var walletClient = ClientB;
            var proposal = await walletClient.Pair(connectData.Uri);

            var approveData = await walletClient.Approve(proposal, testAddress);

            var sessionData = await connectData.Approval;
            await approveData.Acknowledged();

            var rnd = new Random();
            var a = rnd.Next(100);
            var b = rnd.Next(100);

            var testData = new TestRequest()
            {
                a = a,
                b = b,
            };

            var pending = new TaskCompletionSource<int>();
            
            // Step 1. Setup event listener for request
            
            // The wallet client will listen for the request with the "test_method" rpc method
            walletClient.Engine.SessionRequestEvents<TestRequest, TestResponse>()
                    .OnRequest += ( requestData) =>
                {
                    var request = requestData.Request;
                    var data = request.Params;

                    requestData.Response = new TestResponse()
                    {
                        result = data.a * data.b
                    };
                    
                    return Task.CompletedTask;
                };

            // The dapp client will listen for the response
            // Normally, we wouldn't do this and just rely on the return value
            // from the dappClient.Engine.Request function call (the response Result or throws an Exception)
            // We do it here for the sake of testing
            dappClient.Engine.SessionRequestEvents<TestRequest, TestResponse>()
                .FilterResponses((r) => r.Topic == sessionData.Topic)
                .OnResponse += (responseData) =>
            {
                var response = responseData.Response;
                
                var data = response.Result;

                pending.SetResult(data.result);

                return Task.CompletedTask;
            };
            
            // 2. Send the request from the dapp client
            var responseReturned = await dappClient.Engine.Request<TestRequest, TestResponse>(sessionData.Topic, testData);
            
            // 3. Wait for the response from the event listener
            var eventResult = await pending.Task.WithTimeout(TimeSpan.FromSeconds(5));
            
            Assert.Equal(eventResult, a * b);
            Assert.Equal(eventResult, testData.a * testData.b);
            Assert.Equal(eventResult, responseReturned.result);
        }

        [Fact] [Trait("Category", "integration")]
        public async Task TestSessionRequestInvalidMethod()
        {
            await _cryptoFixture.WaitForClientsReady();

            var validMethod = "test_method";

            var dappConnectOptions = new ConnectOptions()
            {
                RequiredNamespaces = new RequiredNamespaces()
                {
                    {
                        "eip155", new ProposedNamespace()
                        {
                            Methods = new[]
                            {
                                validMethod
                            },
                            Chains = new[]
                            {
                                "eip155:1",
                                "eip155:10"
                            },
                            Events = new[]
                            {
                                "chainChanged",
                                "accountsChanged"
                            }
                        }
                    }
                }
            };

            var dappClient = ClientA;
            var connectData = await dappClient.Connect(dappConnectOptions);

            var walletClient = ClientB;
            var proposal = await walletClient.Pair(connectData.Uri);

            var approveData = await walletClient.Approve(proposal, "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045");

            var sessionData = await connectData.Approval;
            await approveData.Acknowledged();

            var testData = new TestRequest2
            {
                x = "test", y = 4
            };

            // Use TestRequest2 which isn't included in the required namespaces
            await Assert.ThrowsAsync<NamespacesException>(() => dappClient.Engine.Request<TestRequest2, TestResponse>(sessionData.Topic, testData));
        }

        [Fact] [Trait("Category", "integration")]
        public async Task TestInvalidConnect()
        {
            await _cryptoFixture.WaitForClientsReady();
            var dappClient = ClientA;

            await Assert.ThrowsAsync<ArgumentNullException>(() => dappClient.Connect(null));

            var connectOptions = new ConnectOptions
            {
                PairingTopic = "123"
            };

            await Assert.ThrowsAsync<KeyNotFoundException>(() => dappClient.Connect(connectOptions));
        }
        
        [Fact, Trait("Category", "integration")]
        public async Task TestTwoUniqueSessionRequestResponse()
        {
            await _cryptoFixture.WaitForClientsReady();
            
            var testAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
            var testMethod = "test_method";
            var testMethod2 = "test_method_2";

            var dappConnectOptions = new ConnectOptions()
            {
                RequiredNamespaces = new RequiredNamespaces()
                {
                    {
                        "eip155", new ProposedNamespace()
                        {
                            Methods = new[]
                            {
                                testMethod,
                                testMethod2
                            },
                            Chains = new[]
                            {
                                "eip155:1", "eip155:10"
                            },
                            Events = new[]
                            {
                                "chainChanged", "accountsChanged"
                            }
                        }
                    }
                }
            };

            var dappClient = ClientA;
            var connectData = await dappClient.Connect(dappConnectOptions);

            var walletClient = ClientB;
            var proposal = await walletClient.Pair(connectData.Uri);

            var approveData = await walletClient.Approve(proposal, testAddress);

            var sessionData = await connectData.Approval;
            await approveData.Acknowledged();

            var rnd = new Random();
            var a = rnd.Next(100);
            var b = rnd.Next(100);
            var x = rnd.NextStrings(AllowedChars, (Math.Min(a, b), Math.Max(a, b)), 1).First();
            var y = x.Length;

            var testData = new TestRequest() { a = a, b = b, };
            var testData2 = new TestRequest2() { x = x, y = y };

            var pending = new TaskCompletionSource<int>();
            var pending2 = new TaskCompletionSource<bool>();
            
            // Step 1. Setup event listener for request
            
            // The wallet client will listen for the request with the "test_method" rpc method
            walletClient.Engine.SessionRequestEvents<TestRequest, TestResponse>()
                    .OnRequest += ( requestData) =>
                {
                    var request = requestData.Request;
                    var data = request.Params;

                    requestData.Response = new TestResponse()
                    {
                        result = data.a * data.b
                    };
                    
                    return Task.CompletedTask;
                };
            
            // The wallet client will listen for the request with the "test_method" rpc method
            walletClient.Engine.SessionRequestEvents<TestRequest2, bool>()
                .OnRequest += ( requestData) =>
            {
                var request = requestData.Request;
                var data = request.Params;

                requestData.Response = data.x.Length == data.y;
                    
                return Task.CompletedTask;
            };

            // The dapp client will listen for the response
            // Normally, we wouldn't do this and just rely on the return value
            // from the dappClient.Engine.Request function call (the response Result or throws an Exception)
            // We do it here for the sake of testing
            dappClient.Engine.SessionRequestEvents<TestRequest, TestResponse>()
                .FilterResponses((r) => r.Topic == sessionData.Topic)
                .OnResponse += (responseData) =>
            {
                var response = responseData.Response;
                
                var data = response.Result;

                pending.TrySetResult(data.result);

                return Task.CompletedTask;
            };
            
            // 2. Send the request from the dapp client
            var responseReturned = await dappClient.Engine.Request<TestRequest, TestResponse>(sessionData.Topic, testData);
            var responseReturned2 = await dappClient.Engine.Request<TestRequest2, bool>(sessionData.Topic, testData2);
            
            // 3. Wait for the response from the event listener
            var eventResult = await pending.Task.WithTimeout(TimeSpan.FromSeconds(5));
            
            Assert.Equal(eventResult, a * b);
            Assert.Equal(eventResult, testData.a * testData.b);
            Assert.Equal(eventResult, responseReturned.result);
            
            Assert.True(responseReturned2);
        }

        [Fact] [Trait("Category", "integration")]
        public async Task TestTwoUniqueComplexSessionRequestResponse()
        {
            await _cryptoFixture.WaitForClientsReady();

            var testAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
            var testMethod = "complex_test_method";
            var testMethod2 = "complex_test_method_2";

            var dappConnectOptions = new ConnectOptions()
            {
                RequiredNamespaces = new RequiredNamespaces()
                {
                    {
                        "eip155", new ProposedNamespace()
                        {
                            Methods = new[]
                            {
                                testMethod,
                                testMethod2
                            },
                            Chains = new[]
                            {
                                "eip155:1",
                                "eip155:10"
                            },
                            Events = new[]
                            {
                                "chainChanged",
                                "accountsChanged"
                            }
                        }
                    }
                }
            };

            var dappClient = ClientA;
            var connectData = await dappClient.Connect(dappConnectOptions);

            var walletClient = ClientB;
            var proposal = await walletClient.Pair(connectData.Uri);

            var approveData = await walletClient.Approve(proposal, testAddress);

            var sessionData = await connectData.Approval;
            await approveData.Acknowledged();

            var rnd = new Random();
            var a = rnd.Next(100);
            var b = rnd.Next(100);
            var x = rnd.NextStrings(AllowedChars, (Math.Min(a, b), Math.Max(a, b)), 1).First();
            var y = x.Length;

            var testData = new ComplexTestRequest(a.ToString(), b.ToString());
            var testData2 = new ComplexTestRequest2(new TestRequest2()
            {
                x = x, y = y
            });

            var pending = new TaskCompletionSource<int>();

            // Step 1. Setup event listener for request

            // The wallet client will listen for the request with the "test_method" rpc method
            walletClient.Engine.SessionRequestEvents<ComplexTestRequest, TestResponse>()
                .OnRequest += (requestData) =>
            {
                var request = requestData.Request;
                var data = request.Params;

                requestData.Response = new TestResponse()
                {
                    result = data.A * data.B
                };

                return Task.CompletedTask;
            };

            // The wallet client will listen for the request with the "test_method" rpc method
            walletClient.Engine.SessionRequestEvents<ComplexTestRequest2, bool>()
                .OnRequest += (requestData) =>
            {
                var request = requestData.Request;
                var data = request.Params;

                requestData.Response = data.X.Length == data.Y;

                return Task.CompletedTask;
            };

            // The dapp client will listen for the response
            // Normally, we wouldn't do this and just rely on the return value
            // from the dappClient.Engine.Request function call (the response Result or throws an Exception)
            // We do it here for the sake of testing
            dappClient.Engine.SessionRequestEvents<ComplexTestRequest, TestResponse>()
                .FilterResponses((r) => r.Topic == sessionData.Topic)
                .OnResponse += (responseData) =>
            {
                var response = responseData.Response;

                var data = response.Result;

                pending.TrySetResult(data.result);

                return Task.CompletedTask;
            };

            // 2. Send the request from the dapp client
            var responseReturned = await dappClient.Engine.Request<ComplexTestRequest, TestResponse>(sessionData.Topic, testData);
            var responseReturned2 = await dappClient.Engine.Request<ComplexTestRequest2, bool>(sessionData.Topic, testData2);

            // 3. Wait for the response from the event listener
            var eventResult = await pending.Task.WithTimeout(TimeSpan.FromSeconds(5));

            Assert.Equal(eventResult, a * b);
            Assert.Equal(eventResult, testData.A * testData.B);
            Assert.Equal(eventResult, responseReturned.result);

            Assert.True(responseReturned2);
        }
        
        [Fact, Trait("Category", "integration")]
        public async Task TestTwoUniqueSessionRequestResponseUsingAddressProviderDefaults()
        {
            await _cryptoFixture.WaitForClientsReady();
            
            var dappClient = ClientA;
            var walletClient = ClientB;

            await dappClient.AddressProvider.LoadDefaultsAsync();
            await walletClient.AddressProvider.LoadDefaultsAsync();
            
            if (!dappClient.AddressProvider.HasDefaultSession && !walletClient.AddressProvider.HasDefaultSession)
            {
                var testAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
                var testMethod = "test_method";
                var testMethod2 = "test_method_2";

                var dappConnectOptions = new ConnectOptions()
                {
                    RequiredNamespaces = new RequiredNamespaces()
                    {
                        {
                            "eip155",
                            new ProposedNamespace()
                            {
                                Methods = new[] { testMethod, testMethod2 },
                                Chains = new[] { "eip155:1" },
                                Events = new[] { "chainChanged", "accountsChanged" }
                            }
                        }
                    }
                };

                var connectData = await dappClient.Connect(dappConnectOptions);

                var proposal = await walletClient.Pair(connectData.Uri);

                var approveData = await walletClient.Approve(proposal, testAddress);

                await connectData.Approval;
                await approveData.Acknowledged();
            }
            else
            {
                Assert.True(dappClient.AddressProvider.HasDefaultSession);
                Assert.True(walletClient.AddressProvider.HasDefaultSession);
            }

            var defaultSessionTopic = dappClient.AddressProvider.DefaultSession.Topic;
            var rnd = new Random();
            var a = rnd.Next(100);
            var b = rnd.Next(100);
            var x = rnd.NextStrings(AllowedChars, (Math.Min(a, b), Math.Max(a, b)), 1).First();
            var y = x.Length;

            var testData = new TestRequest() { a = a, b = b, };
            var testData2 = new TestRequest2() { x = x, y = y };

            var pending = new TaskCompletionSource<int>();
            var pending2 = new TaskCompletionSource<bool>();
            
            // Step 1. Setup event listener for request
            
            // The wallet client will listen for the request with the "test_method" rpc method
            walletClient.Engine.SessionRequestEvents<TestRequest, TestResponse>()
                    .OnRequest += ( requestData) =>
                {
                    var request = requestData.Request;
                    var data = request.Params;

                    requestData.Response = new TestResponse()
                    {
                        result = data.a * data.b
                    };
                    
                    return Task.CompletedTask;
                };
            
            // The wallet client will listen for the request with the "test_method" rpc method
            walletClient.Engine.SessionRequestEvents<TestRequest2, bool>()
                .OnRequest += ( requestData) =>
            {
                var request = requestData.Request;
                var data = request.Params;

                requestData.Response = data.x.Length == data.y;
                    
                return Task.CompletedTask;
            };

            // The dapp client will listen for the response
            // Normally, we wouldn't do this and just rely on the return value
            // from the dappClient.Engine.Request function call (the response Result or throws an Exception)
            // We do it here for the sake of testing
            dappClient.Engine.SessionRequestEvents<TestRequest, TestResponse>()
                .FilterResponses((r) => r.Topic == defaultSessionTopic)
                .OnResponse += (responseData) =>
            {
                Assert.True(responseData.Topic == defaultSessionTopic);
                
                var response = responseData.Response;
                
                var data = response.Result;

                pending.TrySetResult(data.result);

                return Task.CompletedTask;
            };
            
            // 2. Send the request from the dapp client
            var responseReturned = await dappClient.Engine.Request<TestRequest, TestResponse>(testData);
            var responseReturned2 = await dappClient.Engine.Request<TestRequest2, bool>(testData2);
            
            // 3. Wait for the response from the event listener
            var eventResult = await pending.Task.WithTimeout(TimeSpan.FromSeconds(5));
            
            Assert.Equal(eventResult, a * b);
            Assert.Equal(eventResult, testData.a * testData.b);
            Assert.Equal(eventResult, responseReturned.result);
            
            Assert.True(responseReturned2);
        }
        
        [Fact, Trait("Category", "integration")]
        public async Task TestAddressProviderDefaults()
        {
            await _cryptoFixture.WaitForClientsReady();
            
            var testAddress = "0xd8dA6BF26964aF9D7eEd9e03E53415D37aA96045";
            var testMethod = "test_method";
            var testMethod2 = "test_method_2";

            var dappConnectOptions = new ConnectOptions()
            {
                RequiredNamespaces = new RequiredNamespaces()
                {
                    {
                        "eip155", new ProposedNamespace()
                        {
                            Methods = new[]
                            {
                                testMethod,
                                testMethod2
                            },
                            Chains = new[]
                            {
                                "eip155:1"
                            },
                            Events = new[]
                            {
                                "chainChanged", "accountsChanged"
                            }
                        }
                    }
                }
            };

            var dappClient = ClientA;
            var connectData = await dappClient.Connect(dappConnectOptions);

            var walletClient = ClientB;
            var proposal = await walletClient.Pair(connectData.Uri);

            var approveData = await walletClient.Approve(proposal, testAddress);

            await connectData.Approval;
            await approveData.Acknowledged();

            var address = dappClient.AddressProvider.CurrentAddress();
            Assert.Equal(testAddress, address.Address);
            Assert.Equal("eip155:1", address.ChainId);
            Assert.Equal("eip155:1", dappClient.AddressProvider.DefaultChainId);
            Assert.Equal("eip155", dappClient.AddressProvider.DefaultNamespace);
            
            address = walletClient.AddressProvider.CurrentAddress();
            Assert.Equal(testAddress, address.Address);
            Assert.Equal("eip155:1", address.ChainId);
            Assert.Equal("eip155:1", dappClient.AddressProvider.DefaultChainId);
            Assert.Equal("eip155", dappClient.AddressProvider.DefaultNamespace);

            var allAddresses = dappClient.AddressProvider.AllAddresses("eip155").ToArray();
            Assert.Single(allAddresses);
            Assert.Equal(testAddress, allAddresses[0].Address);
            Assert.Equal("eip155:1", allAddresses[0].ChainId);
        }

        [Fact, Trait("Category", "integration")]
        public async Task TestAddressProviderDefaultsSaving()
        {
            await _cryptoFixture.WaitForClientsReady();

            await TestTwoUniqueSessionRequestResponseUsingAddressProviderDefaults();

            var defaultSessionTopic = _cryptoFixture.ClientA.AddressProvider.DefaultSession.Topic;

            Assert.NotNull(defaultSessionTopic);

            _cryptoFixture.StorageOverrideA = _cryptoFixture.ClientA.Core.Storage;
            _cryptoFixture.StorageOverrideB = _cryptoFixture.ClientB.Core.Storage;

            await Task.Delay(500);
            
            await _cryptoFixture.DisposeAndReset();
            
            await Task.Delay(500);

            await _cryptoFixture.ClientA.AddressProvider.LoadDefaultsAsync();
            var reloadedDefaultSessionTopic = _cryptoFixture.ClientA.AddressProvider.DefaultSession.Topic;
            
            Assert.Equal(defaultSessionTopic, reloadedDefaultSessionTopic);
            
            await TestTwoUniqueSessionRequestResponseUsingAddressProviderDefaults();
        }

        [Fact] [Trait("Category", "integration")]
        public async Task TestAddressProviderChainIdChange()
        {
            await _cryptoFixture.WaitForClientsReady();

            _ = await TestConnectMethod(ClientA, ClientB);

            const string badChainId = "badChainId";
            await Assert.ThrowsAsync<ArgumentException>(() => ClientA.AddressProvider.SetDefaultChainIdAsync(badChainId));

            // Change the default chain id
            const string newChainId = "eip155:10";
            await ClientA.AddressProvider.SetDefaultChainIdAsync(newChainId);
            Assert.Equal(newChainId, ClientA.AddressProvider.DefaultChainId);
        }

        [Fact] [Trait("Category", "integration")]
        public async Task TestAddressProviderDisconnect()
        {
            await _cryptoFixture.WaitForClientsReady();

            _ = await TestConnectMethod(ClientA, ClientB);

            Assert.True(ClientA.AddressProvider.HasDefaultSession);

            await ClientA.Disconnect();

            Assert.False(ClientA.AddressProvider.HasDefaultSession);
        }
    }
}
