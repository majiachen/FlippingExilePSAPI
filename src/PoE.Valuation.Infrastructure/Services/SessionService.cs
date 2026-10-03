using Microsoft.Extensions.Options;
using PoE.Valuation.Core.Configuration;
using PoE.Valuation.Core.Logging;
using PoE.Valuation.Core.OAuth;
using PoE.Valuation.Core.Redis;
using PoE.Valuation.Infrastructure.Clients;

namespace PoE.Valuation.Infrastructure.Services;

/// <summary>
/// Orchestrates the PKCE OAuth login flow and the lifetime of user sessions (tech doc, section 5).
/// Sessions live in Redis (see <see cref="ISessionStore"/>); the PoE access token is refreshed
/// transparently via <see cref="ISessionStore.ReplaceAsync"/> so a failed refresh never logs a user
/// out. Registered as scoped: it depends on the transient <see cref="PoeOAuthClient"/> typed client.
/// </summary>
public sealed class SessionService
{
    private readonly ISessionStore _sessions;
    private readonly IRedisStore _redis;
    private readonly PoeOAuthClient _oauth;
    private readonly SensitiveValueStore _sensitive;
    private readonly OAuthOptions _oauthOptions;
    private readonly RedisOptions _redisOptions;

    public SessionService(
        ISessionStore sessions,
        IRedisStore redis,
        PoeOAuthClient oauth,
        SensitiveValueStore sensitive,
        IOptions<OAuthOptions> oauthOptions,
        IOptions<RedisOptions> redisOptions)
    {
        _sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _oauth = oauth ?? throw new ArgumentNullException(nameof(oauth));
        _sensitive = sensitive ?? throw new ArgumentNullException(nameof(sensitive));
        _oauthOptions = oauthOptions?.Value ?? throw new ArgumentNullException(nameof(oauthOptions));
        _redisOptions = redisOptions?.Value ?? throw new ArgumentNullException(nameof(redisOptions));
    }

    /// <summary>
    /// Generates a fresh PKCE pair, stores the code verifier under <c>oauth:state:{state}</c>
    /// (10-minute TTL) and returns the PoE authorization URL the client should be redirected to.
    /// </summary>
    public async Task<Uri> BeginLoginAsync(CancellationToken ct = default)
    {
        var pair = OAuthPkce.Generate();
        _sensitive.Add(pair.State);
        _sensitive.Add(pair.CodeVerifier);

        await _redis.SetAsync(
            RedisKeys.OAuthState(pair.State),
            new OAuthStateEntry(pair.CodeVerifier, DateTimeOffset.UtcNow),
            _redisOptions.OAuthStateTtl,
            ct);

        return OAuthPkce.BuildAuthorizationUri(
            _oauthOptions.AuthorizationEndpoint,
            _oauthOptions.ClientId,
            _oauthOptions.RedirectUri,
            _oauthOptions.Scope,
            pair.State,
            pair.CodeChallenge);
    }

    /// <summary>
    /// Completes the OAuth callback: validates and consumes the one-time state, exchanges the
    /// authorization code for tokens, persists a new session and returns its id (to be written to the
    /// <c>poe_sid</c> cookie). Returns <see cref="CallbackResult.Failure"/> on any validation or
    /// token-exchange error.
    /// </summary>
    public async Task<CallbackResult> HandleCallbackAsync(string? code, string? state, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code) || !OAuthPkce.IsValidState(state))
            return CallbackResult.Failure;

        var key = RedisKeys.OAuthState(state!); // non-null: IsValidState above guarantees a well-formed state
        var stateEntry = await _redis.GetAsync<OAuthStateEntry>(key, ct);
        await _redis.DeleteAsync(key, ct); // single-use: the state can never be replayed
        if (stateEntry is null)
            return CallbackResult.Failure;

        OAuthTokenResponse token;
        try
        {
            token = await _oauth.ExchangeAuthorizationCodeAsync(
                _oauthOptions.TokenEndpoint,
                code,
                stateEntry.CodeVerifier,
                _oauthOptions.ClientId,
                _oauthOptions.ClientSecret,
                _oauthOptions.RedirectUri,
                ct);
        }
        catch
        {
            return CallbackResult.Failure;
        }

        var sessionId = Guid.NewGuid().ToString("n");
        var entry = new SessionEntry(
            token.AccessToken,
            token.RefreshToken ?? string.Empty,
            DateTimeOffset.UtcNow.AddSeconds(token.ExpiresIn),
            token.Sub,
            token.Username,
            DateTimeOffset.UtcNow);
        await _sessions.SetAsync(sessionId, entry, ct);
        return new CallbackResult.Success(sessionId);
    }

    /// <summary>
    /// Loads the session for <paramref name="sessionId"/>, transparently refreshing the PoE access
    /// token with the stored refresh token when it is expired or within a one-minute margin. Returns
    /// null when no such session exists (the client must re-authenticate). A failed refresh returns
    /// the current (possibly stale) entry rather than logging the user out.
    /// </summary>
    public async Task<SessionEntry?> GetUserSessionAsync(string sessionId, CancellationToken ct = default)
    {
        var entry = await _sessions.GetAsync(sessionId, ct);
        if (entry is null)
            return null;

        if (entry.AccessTokenExpiresAt > DateTimeOffset.UtcNow.AddMinutes(1))
            return entry;

        if (string.IsNullOrEmpty(entry.RefreshToken))
            return entry; // nothing to refresh with; any dead token surfaces as a PoE 401

        try
        {
            var refreshed = await _oauth.RefreshTokenAsync(
                _oauthOptions.TokenEndpoint, entry.RefreshToken,
                _oauthOptions.ClientId, _oauthOptions.ClientSecret, ct);

            var updated = entry with
            {
                AccessToken = refreshed.AccessToken,
                // Persist a rotated refresh token when provided; otherwise keep the existing one.
                RefreshToken = string.IsNullOrEmpty(refreshed.RefreshToken) ? entry.RefreshToken : refreshed.RefreshToken,
                AccessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(refreshed.ExpiresIn)
            };
            await _sessions.ReplaceAsync(sessionId, updated, ct);
            return updated;
        }
        catch
        {
            return entry;
        }
    }

    /// <summary>Deletes the session for <paramref name="sessionId"/> (no-op when it does not exist).</summary>
    public Task LogoutAsync(string sessionId, CancellationToken ct = default) =>
        _sessions.DeleteAsync(sessionId, ct);

    /// <summary>Outcome of <see cref="HandleCallbackAsync"/>.</summary>
    public abstract record CallbackResult
    {
        /// <summary>Login succeeded; the client should set the session cookie to <see cref="SessionId"/>.</summary>
        public sealed record Success(string SessionId) : CallbackResult;

        /// <summary>Login failed (invalid/expired state, or the token exchange was rejected).</summary>
        public static readonly CallbackResult Failure = new FailureResult();

        private sealed record FailureResult : CallbackResult;
    }
}