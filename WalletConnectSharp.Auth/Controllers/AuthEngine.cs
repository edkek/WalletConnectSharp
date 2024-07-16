using Newtonsoft.Json;
using WalletConnectSharp.Auth.Interfaces;
using WalletConnectSharp.Auth.Internals;
using WalletConnectSharp.Auth.Models;
using WalletConnectSharp.Common.Model.Errors;
using WalletConnectSharp.Common.Utils;
using WalletConnectSharp.Core;
using WalletConnectSharp.Core.Models;
using WalletConnectSharp.Core.Models.Verify;
using WalletConnectSharp.Crypto.Models;
using WalletConnectSharp.Network.Models;
using ErrorResponse = WalletConnectSharp.Auth.Models.ErrorResponse;

namespace WalletConnectSharp.Auth.Controllers;

public partial class AuthEngine : IAuthEngine
{
    private bool initialized = false;
    
    
    public const string AUTH_CLIENT_PROTOCOL = "wc";
    public const int AUTH_CLIENT_VERSION = 1;
    public const string AUTH_CLIENT_CONTEXT = "auth";

    public static readonly string AUTH_CLIENT_STORAGE_PREFIX =
        $"{AUTH_CLIENT_PROTOCOL}@{AUTH_CLIENT_VERSION}:{AUTH_CLIENT_CONTEXT}";

    public static readonly string AUTH_CLIENT_PUBLIC_KEY_NAME = $"{AUTH_CLIENT_STORAGE_PREFIX}:PUB_KEY";
    
    private DisposeHandlerToken messageHandler;
    protected bool Disposed;

    public string Name
    {
        get
        {
            return "authEngine";
        }
    }

    public string Context
    {
        get
        {
            return $"{Name}-context";
        }
    }

    public IAuthClient Client { get; }

    public IDictionary<long, PendingRequest> PendingRequests
    {
        get
        {
            return this.Client.Requests.Values.OfType<PendingRequest>().Where(obj => obj.Id != null).GroupBy(obj => (long)obj.Id).ToDictionary(x => x.Key, x => x.First());
        }
    }

    public AuthEngine(IAuthClient client)
    {
        Client = client;
    }

    public async Task Init()
    {
        if (!initialized)
        {
            await RegisterRelayerEvents();
            this.initialized = true;
        }
    }

    public async Task<RequestUri> Request(RequestParams @params, string topic = null)
    {
        IsInitialized();

        @params.Type ??= "eip4361";

        if (!IsValidRequest(@params))
        {
            throw new ArgumentException("Invalid request", nameof(@params));
        }

        if (topic != null)
        {
            return await this.RequestOnKnownPairing(topic, @params);
        }

        var data = await this.Client.Core.Pairing.Create();
        var pairingTopic = data.Topic;
        var uri = data.Uri;
        
        // TODO Log

        var publicKey = await this.Client.Core.Crypto.GenerateKeyPair();
        var responseTopic = this.Client.Core.Crypto.HashKey(publicKey);

        await this.Client.AuthKeys.Set(AUTH_CLIENT_PUBLIC_KEY_NAME,
            new AuthData() { PublicKey = publicKey, ResponseTopic = responseTopic });
        await this.Client.PairingTopics.Set(responseTopic,
            new PairingData() { Topic = responseTopic, PairingTopic = pairingTopic });

        this.Client.Core.MessageHandler.SetDecodeOptionsForTopic(new DecodeOptions()
        {
            ReceiverPublicKey = publicKey
        }, responseTopic);
        
        await this.Client.Core.Relayer.Subscribe(responseTopic);
        
        // TODO Log

        var id = await this.SendRequest(pairingTopic, new WcAuthRequest()
        {
            Payload = new PayloadParams()
            {
                Type = @params.Type ?? "eip4361",
                ChainId = @params.ChainId,
                Statement = @params.Statement,
                Aud = @params.Aud,
                Domain = @params.Domain,
                Version = "1",
                Nonce = @params.Nonce,
                Iat = DateTime.Now.ToISOString(),
            },
            Requester = new Requester() { Metadata = this.Client.Metadata, PublicKey = publicKey }
        }, @params.Expiry);
        
        // TODO Log

        return new RequestUri() { Uri = uri, Id = id };
    }

    private async Task<RequestUri> RequestOnKnownPairing(string topic, RequestParams @params)
    {
        var knownPairing = this.Client.Core.Pairing.Pairings.First(p => p.Active.HasValue && p.Active.Value && p.Topic == topic);

        var publicKey = this.Client.AuthKeys.Get(AUTH_CLIENT_PUBLIC_KEY_NAME);

        var id = await this.SendRequest(knownPairing.Topic,
            new WcAuthRequest()
            {
                Payload = new PayloadParams()
                {
                    Type = @params.Type ?? "eip4361",
                    ChainId = @params.ChainId,
                    Statement = @params.Statement,
                    Aud = @params.Aud,
                    Domain = @params.Domain,
                    Version = "1",
                    Nonce = @params.Nonce,
                    Iat = DateTime.Now.ToISOString()
                },
                Requester = new Requester() { PublicKey = publicKey.PublicKey, Metadata = this.Client.Metadata }
            }, @params.Expiry);

        // TODO Log
        
        return new RequestUri() { Id = id };
    }

    public async Task Respond(Message message, string iss)
    {
        this.IsInitialized();

        if (message.Id == null || !IsValidRespond(message, this.Client.Requests))
        {
            throw new Exception("Invalid response");
        }

        var pendingRequest = GetPendingRequest(this.Client.Requests, (long)message.Id);

        if (pendingRequest == null || pendingRequest.Id == null)
        {
            throw new Exception("Invalid pending request stored");
        }

        var id = (long)pendingRequest.Id;
        var receiverPublicKey = pendingRequest.Requester.PublicKey;
        var senderPublicKey = await this.Client.Core.Crypto.GenerateKeyPair();
        var responseTopic = this.Client.Core.Crypto.HashKey(receiverPublicKey);
        var encodeOptions = new EncodeOptions()
        {
            Type = Crypto.Crypto.Type1, ReceiverPublicKey = receiverPublicKey, SenderPublicKey = senderPublicKey
        };

        Cacao cacao;
        switch (message)
        {
            case AuthErrorResponse errorResponse:
                await this.SendError(id, responseTopic,
                    new ErrorResponse() { Error = errorResponse.Error, Id = errorResponse.Id }, encodeOptions);
                return;
            case ErrorResponse errorResponse:
                await this.SendError(id, responseTopic, errorResponse, encodeOptions);
                return;
            case Cacao cacao1:
                cacao = cacao1;
                cacao.Payload ??= new Cacao.CacaoPayload(pendingRequest.CacaoPayload) { Iss = iss };
                break;
            case ResultResponse response:
                cacao = new Cacao()
                {
                    Payload = new Cacao.CacaoPayload(pendingRequest.CacaoPayload) { Iss = iss },
                    Signature = response.Signature
                };
                break;
            default:
                throw new ArgumentException(
                    $"Unknown message type {message.GetType()}, expected Cacao or ResultResponse");
        }

        await this.SendResult(id, responseTopic, cacao, encodeOptions);

        await this.Client.Requests.Update(id, cacao);
    }

    protected Task<long> SendRequest(string topic, WcAuthRequest request, long? expiry = null, EncodeOptions options = null)
    {
        return this.Client.Core.MessageHandler.SendRequest<WcAuthRequest, Cacao>(topic, request, expiry, options);
    }

    protected Task SendError(long id, string topic, ErrorResponse response, EncodeOptions options = null)
    {
        return this.Client.Core.MessageHandler.SendError<WcAuthRequest, Cacao>(id, topic, response.Error, options);
    }

    protected Task SendResult(long id, string topic, Cacao result, EncodeOptions options = null)
    {
        return this.Client.Core.MessageHandler.SendResult<WcAuthRequest, Cacao>(id, topic, result, options);
    }

    protected async Task SetExpiry(string topic, long expiry)
    {
        if (this.Client.Core.Pairing.Store.Keys.Contains(topic))
        {
            await this.Client.Core.Pairing.UpdateExpiry(topic, expiry);
        }
        this.Client.Core.Expirer.Set(topic, expiry);
    }

    private async Task RegisterRelayerEvents()
    {
        // MessageHandler will handle all topic tracking
        this.messageHandler = await this.Client.Core.MessageHandler.HandleMessageType<WcAuthRequest, Cacao>(OnAuthRequest, OnAuthResponse);
    }

    private async Task OnAuthResponse(string topic, JsonRpcResponse<Cacao> response)
    {
        var id = response.Id;

        if (!response.IsError)
        {
            var pairing = this.Client.PairingTopics.Get(topic);
            await this.Client.Core.Pairing.Activate(pairing.PairingTopic);
            
            var signature = response.Result.Signature;
            var payload = response.Result.Payload;

            await this.Client.Requests.Set(id, response.Result);
            var reconstructed = FormatMessage(payload);
            
            // TODO Log

            var walletAddress = IssDidUtils.DidAddress(payload.Iss);
            var chainId = IssDidUtils.DidChainId(payload.Iss);

            if (string.IsNullOrWhiteSpace(walletAddress))
            {
                throw new ArgumentException("Could not derive address from iss in payload");
            }

            if (string.IsNullOrWhiteSpace(chainId))
            {
                throw new ArgumentException("Could not derive chainId from iss in payload");
            }

            var isValid = await SignatureUtils.VerifySignature(walletAddress, reconstructed, signature, chainId,
                this.Client.ProjectId);

            if (!isValid)
            {
                this.Client.OnAuthResponse(new AuthErrorResponse()
                {
                    Id = id, Topic = topic, Error = Error.FromErrorType(ErrorType.GENERIC, "Invalid signature")
                });
            }
            else
            {
                this.Client.OnAuthResponse(new AuthResponse() { Id = id, Topic = topic, Response = response });
            }
        }
        else
        {
            this.Client.OnAuthResponse(new AuthErrorResponse() { Id = id, Topic = topic, Error = response.Error });
        }
    }

    private async Task OnAuthRequest(string topic, JsonRpcRequest<WcAuthRequest> payload)
    {
        var payloadParams = payload.Params.Payload;
        var cacaoPayload = new Cacao.CacaoRequestPayload()
        {
            Aud = payloadParams.Aud,
            ChainId = payloadParams.ChainId,
            Domain = payloadParams.Domain,
            Exp = payloadParams.Exp,
            Iat = payloadParams.Iat,
            Nbf = payloadParams.Nbf,
            Nonce = payloadParams.Nonce,
            RequestId = payloadParams.RequestId,
            Resource = payloadParams.Resources,
            Statement = payloadParams.Statement
        };

        await this.Client.Requests.Set(payload.Id,
            new PendingRequest()
            {
                CacaoPayload = cacaoPayload,
                Id = payload.Id,
                PairingTopic = topic,
                Requester = payload.Params.Requester
            });

        var hash = HashUtils.HashMessage(JsonConvert.SerializeObject(payload));
        var verifyContext = await GetVerifyContext(hash, this.Client.Metadata);

        this.Client.OnAuthRequest(new AuthRequest()
        {
            Id = payload.Id,
            Topic = topic,
            Parameters =
                new AuthRequestData() { CacaoPayload = cacaoPayload, Requester = payload.Params.Requester },
            VerifyContext = verifyContext
        });
    }

    private async Task<VerifiedContext> GetVerifyContext(string hash, Metadata metadata)
    {
        var context = new VerifiedContext()
        {
            VerifyUrl = metadata.VerifyUrl, Validation = Validation.Unknown, Origin = metadata.Url
        };
        try
        {

            var origin = await this.Client.Core.Verify.Resolve(hash);

            if (string.IsNullOrWhiteSpace(origin))
            {
                return context;
            }

            context.Origin = origin;
            context.Validation = origin == metadata.Url ? Validation.Valid : Validation.Invalid;
        }
        catch (Exception e)
        {
            // TODO Log exception
        }

        return context;
    }

    public string FormatMessage(Cacao.CacaoPayload cacao)
    {
        string iss = cacao.Iss;
        var header = $"{cacao.Domain} wants you to sign in with your Ethereum account:";
        var walletAddress = IssDidUtils.DidAddress(iss) + "\n" + (cacao.Statement != null ? "" : "\n");
        var statement = cacao.Statement + "\n";
        var uri = $"URI: {cacao.Aud}";
        var version = $"Version: {cacao.Version}";
        var chainId = $"Chain ID: {IssDidUtils.DidChainId(iss)}";
        var nonce = $"Nonce: {cacao.Nonce}";
        var issuedAt = $"Issued At: {cacao.Iat}";
        var resources = cacao.Resource != null && cacao.Resource.Length > 0
            ? $"Resources:\n{string.Join('\n', cacao.Resource.Select((resource) => $"- {resource}"))}"
            : null;

        var message = string.Join('\n',
            new string[] { header, walletAddress, statement, uri, version, chainId, nonce, issuedAt, resources }
                .Where(val => !string.IsNullOrWhiteSpace(val)));

        return message;
    }

    private void IsInitialized()
    {
        if (!this.initialized)
        {
            throw new InvalidOperationException($"{nameof(AuthEngine)} module not initialized.");
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (Disposed) return;
            
        if (disposing)
        {
            this.messageHandler.Dispose();
        }

        Disposed = true;
    }
}
