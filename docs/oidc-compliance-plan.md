# OIDC Compliance Implementation Plan

**Project:** CoreDesign.Identity.Server  
**Target:** OpenID Connect Core 1.0 certification (Basic OP, Config OP, Dynamic OP profiles)  
**Date:** 2026-05-06  
**Author:** Cody Skidmore  
**Status:** Milestone 1 complete (branch: `oidc/milestone-1-client-store`). Next: Milestone 2 (Token Separation).

---

## 1. Scope

This plan covers the work required to evolve `CoreDesign.Identity.Server` from its current partial state into a fully compliant OpenID Connect Provider (OP) capable of passing the OpenID Foundation Conformance Test Suite and earning a certified listing at openid.net.

**In scope:**

- OpenID Connect Core 1.0
- OpenID Connect Discovery 1.0
- OpenID Connect Dynamic Client Registration 1.0 (RFC 7591)
- OAuth 2.0 Authorization Framework (RFC 6749) — Authorization Code grant only
- Proof Key for Code Exchange (RFC 7636, PKCE)
- OAuth 2.0 Token Revocation (RFC 7009)
- OAuth 2.0 Token Introspection (RFC 7762)
- OAuth 2.0 Bearer Token Usage (RFC 6750)

**Out of scope:**

- SAML 2.0
- OAuth 2.0 Implicit Flow (deprecated in OAuth 2.1, not tested in modern conformance profiles)
- OAuth 2.0 Hybrid Flow
- OpenID Connect Session Management 1.0 (iframe-based, browser-only)
- OpenID Connect Back-Channel Logout 1.0
- OpenID Connect Front-Channel Logout 1.0

---

## 2. Certification Targets

The OpenID Foundation runs a self-certification program at `certification.openid.net`. Certification is profile-based. The three profiles targeted by this plan are listed below in the order they will be achieved.

| Profile | What it covers | Achievable after milestone |
|---|---|---|
| Config OP | Full discovery document compliance | 4 |
| Basic OP | Authorization Code Flow with PKCE, ID Token, UserInfo | 5 |
| Dynamic OP | Dynamic Client Registration (RFC 7591) | 8 |

Passing all three profiles earns a "Basic, Config, Dynamic" certified listing. This is the standard certification held by major providers such as Keycloak and Dex.

---

## 3. Current State

The table below audits every required OIDC component against what exists today.

| Component | Status | Notes |
|---|---|---|
| `/.well-known/openid-configuration` | Partial | Several required fields missing (`code_challenge_methods_supported`, `end_session_endpoint`, etc.) |
| `/.well-known/jwks.json` | Complete | RS256, correct JWK format |
| `/connect/token` | Partial | ROPC grant only, no `client_id` validation, same JWT issued for both `access_token` and `id_token` |
| `/connect/userinfo` | Partial | No scope-based claim filtering, no `WWW-Authenticate` header on 401 |
| `/connect/authorize` | Missing | Required for Authorization Code Flow |
| `/connect/endsession` | Missing | Required for compliant logout |
| `/connect/revocation` | Missing | RFC 7009 |
| `/connect/introspect` | Missing | RFC 7762 |
| `/connect/register` | Missing | RFC 7591 Dynamic Registration |
| Client store | Missing | No `client_id` concept exists |
| Authorization code store | Missing | Required for code flow |
| Refresh token store | Missing | Required for `offline_access` scope |
| Token separation | Missing | `access_token` and `id_token` are the same JWT |
| PKCE | Missing | `code_challenge` / `code_verifier` not implemented |
| `nonce` claim | Missing | Required in ID Token when provided in authorization request |
| `auth_time` claim | Missing | Required in ID Token |
| `at_hash` claim | Missing | Required in ID Token when access token is issued alongside |
| `/auth/login` (dev helper) | Present | Not an OIDC endpoint, remains for development convenience only |

---

## 4. Architecture Overview

The diagram below shows all new components introduced across the milestones.

```
CoreDesign.Identity.Server
├── Clients/
│   ├── ClientRecord.cs              (model)
│   ├── IClientStore.cs              (interface)
│   └── JsonFileClientStore.cs       (reads clients.json)
├── AuthorizationCodes/
│   ├── AuthorizationCode.cs         (model)
│   ├── IAuthorizationCodeStore.cs   (interface)
│   └── InMemoryAuthorizationCodeStore.cs
├── RefreshTokens/
│   ├── RefreshTokenRecord.cs        (model)
│   ├── IRefreshTokenStore.cs        (interface)
│   └── InMemoryRefreshTokenStore.cs
├── Pkce/
│   └── PkceValidator.cs             (S256 verifier helper)
├── Features/
│   ├── Authorize/
│   │   └── AuthorizeEndpoint.cs     (GET + POST /connect/authorize)
│   ├── EndSession/
│   │   └── EndSessionEndpoint.cs    (GET /connect/endsession)
│   ├── Revocation/
│   │   └── RevocationEndpoint.cs    (POST /connect/revocation)
│   ├── Introspection/
│   │   └── IntrospectionEndpoint.cs (POST /connect/introspect)
│   └── Registration/
│       └── RegistrationEndpoint.cs  (POST /connect/register)
├── TokenBuilder.cs                  (updated: two separate tokens)
└── IdTokenBuilder.cs                (new: ID Token specific logic)
```

---

## 5. Milestones

Each milestone is self-contained. The server builds and runs correctly at every milestone boundary. No milestone leaves the server in a broken or half-implemented state.

---

### Milestone 1: Client Store

**Goal:** Introduce a registered-client concept so the server knows which applications are allowed to request tokens.

**What to build:**

`ClientRecord.cs` — the model for a registered Relying Party:

```csharp
public sealed class ClientRecord
{
    public string ClientId { get; init; } = string.Empty;
    public string? ClientSecret { get; init; }
    public string TokenEndpointAuthMethod { get; init; } = "none";
    public List<string> AllowedGrantTypes { get; init; } = [];
    public List<string> AllowedRedirectUris { get; init; } = [];
    public List<string> AllowedPostLogoutRedirectUris { get; init; } = [];
    public List<string> AllowedScopes { get; init; } = [];
    public bool RequirePkce { get; init; } = true;
}
```

`IClientStore.cs`:

```csharp
public interface IClientStore
{
    Task<ClientRecord?> FindByClientIdAsync(string clientId);
}
```

`JsonFileClientStore.cs` — reads from `clients.json`, same pattern as `JsonFileIdentityStore`.

`clients.json` (sample, placed alongside `identities.json`):

```json
[
  {
    "clientId": "sampleapi-frontend",
    "tokenEndpointAuthMethod": "none",
    "allowedGrantTypes": ["authorization_code"],
    "allowedRedirectUris": ["https://localhost:3000/callback"],
    "allowedPostLogoutRedirectUris": ["https://localhost:3000"],
    "allowedScopes": ["openid", "profile", "email"],
    "requirePkce": true
  }
]
```

Registration in `IdentityExtensions.cs`:

```csharp
public static IServiceCollection AddJsonFileClientStore(
    this IServiceCollection services, string filePath) { ... }
```

Update `/connect/token` to reject requests where `client_id` is absent or not found in the store.

Update discovery to advertise `token_endpoint_auth_methods_supported: ["none", "client_secret_post"]`.

**Definition of done:** `/connect/token` returns `invalid_client` for any request without a valid `client_id`. The store loads from `clients.json` and the new `AddJsonFileClientStore` call appears in `SampleApi.Identity.Api`'s `Program.cs`.

---

### Milestone 2: Token Separation

**Goal:** Issue distinct access tokens and ID tokens. These are different JWTs with different claims, audiences, and lifetimes.

**What to build:**

Split `TokenBuilder` into two builders:

`AccessTokenBuilder.Build(identity, client, creds, options)` — issues a short-lived JWT containing `sub`, `jti`, `iat`, `exp`, `iss`, `aud` (API audience), `scope`, `roles`, and `client_id`. Does not contain profile claims.

`IdTokenBuilder.Build(identity, client, accessToken, nonce, authTime, creds, options)` — issues an ID Token containing `sub`, `iss`, `aud` (client_id), `exp`, `iat`, `auth_time`, optional `nonce`, and `at_hash` (SHA-256 of the left half of the base64url-encoded access token).

`at_hash` computation:

```csharp
var tokenBytes = Encoding.ASCII.GetBytes(accessToken);
var hashBytes = SHA256.HashData(tokenBytes);
var leftHalf = hashBytes[..16];
return Base64UrlEncoder.Encode(leftHalf);
```

Update `IdentityOptions` to add separate lifetime fields:

```json
"CoreDesign": {
  "Identity": {
    "Issuer": "...",
    "Audience": "...",
    "AccessTokenLifetimeMinutes": 15,
    "IdTokenLifetimeMinutes": 60,
    "RefreshTokenLifetimeDays": 30
  }
}
```

**Definition of done:** `/connect/token` returns two distinct JWTs. Decoding both at jwt.io shows that the access token contains `aud` pointing to the API and the ID Token contains `aud` equal to `client_id`, and the ID Token's `at_hash` validates against the access token.

---

### Milestone 3: PKCE Infrastructure

**Goal:** Implement PKCE validation as a standalone, reusable component before wiring it into the authorization flow. PKCE (RFC 7636) prevents authorization code interception attacks.

**What to build:**

`PkceValidator.cs`:

```csharp
internal static class PkceValidator
{
    internal static bool Verify(string codeVerifier, string codeChallenge, string method)
    {
        if (method != "S256")
            return false;
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
        var computed = Base64UrlEncoder.Encode(hash);
        return computed == codeChallenge;
    }
}
```

`AuthorizationCode.cs` — the model stored server-side when an auth code is issued:

```csharp
public sealed class AuthorizationCode
{
    public string Code { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string RedirectUri { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public string? Nonce { get; init; }
    public string CodeChallenge { get; init; } = string.Empty;
    public string CodeChallengeMethod { get; init; } = "S256";
    public DateTime AuthTime { get; init; }
    public DateTime ExpiresAt { get; init; }
}
```

`IAuthorizationCodeStore.cs` and `InMemoryAuthorizationCodeStore.cs` — stores codes with a 60-second TTL and consumes them on first use (single-use enforcement).

**Definition of done:** PKCE verification logic passes unit tests with valid and invalid code verifiers. The code store correctly expires and single-use enforces codes.

---

### Milestone 4: Authorization Endpoint and Code Exchange

**Goal:** Implement the full Authorization Code Flow. This is the largest milestone and the heart of OIDC.

**What to build:**

`AuthorizeEndpoint.cs` — `GET /connect/authorize`:

Required parameters: `client_id`, `redirect_uri`, `response_type` (must be `code`), `scope` (must include `openid`), `state`.

Optional parameters: `code_challenge`, `code_challenge_method`, `nonce`, `prompt`, `max_age`.

Validation sequence:

1. Load client from store. If not found, return error page (do not redirect).
2. Validate `redirect_uri` exactly matches one of the client's registered URIs. If not, return error page.
3. Validate `response_type` equals `code`.
4. Validate `scope` contains `openid`.
5. If `client.RequirePkce` is true, require `code_challenge` and `code_challenge_method=S256`.
6. Issue an authorization code, store it with all flow parameters.
7. Redirect to `redirect_uri?code={code}&state={state}`.

For the development server, this endpoint needs to know who the user is. Since the dev server has no login UI, the authorization endpoint accepts HTTP Basic credentials (`Authorization: Basic base64(user:pass)`) and validates them against `IIdentityStore`. This is non-standard and development-only, but keeps the server functional without a full login page.

A production-ready or browser-based login UI is a future enhancement documented in the extensions section below.

Update `/connect/token` to handle `grant_type=authorization_code`:

1. Validate `client_id`, `code`, `redirect_uri`.
2. Load and consume the authorization code from the store. Return `invalid_grant` if not found, expired, or already used.
3. Validate `redirect_uri` matches the URI stored with the code.
4. Validate PKCE: compute S256 of `code_verifier`, compare to stored `code_challenge`.
5. Issue access token and ID token via the two builders from Milestone 2.
6. If `offline_access` is in the scope, issue a refresh token (Milestone 6).

Update discovery to add:
- `authorization_endpoint`
- `code_challenge_methods_supported: ["S256"]`
- `response_types_supported: ["code"]` (remove `token` and `id_token` since those are implicit variants)
- `grant_types_supported: ["authorization_code"]`

**Definition of done:** A full Authorization Code Flow with PKCE completes end-to-end. `client_id` and `redirect_uri` are validated against the client store. A bad `code_verifier` returns `invalid_grant`. The Config OP conformance profile passes at `certification.openid.net`.

---

### Milestone 5: UserInfo Endpoint Compliance

**Goal:** Bring the existing UserInfo endpoint to full spec compliance. The endpoint already exists; this milestone tightens it.

**Changes:**

Scope-based claim filtering. The UserInfo response must contain only claims corresponding to the scopes in the access token.

| Scope | Claims returned |
|---|---|
| `openid` | `sub` |
| `profile` | `name`, `given_name`, `family_name`, `preferred_username` |
| `email` | `email` |
| `phone` | `phone_number` (if present) |
| `address` | `address` (if present) |

The endpoint must extract the scope from the validated access token and filter accordingly.

The 401 response must include a `WWW-Authenticate` header per RFC 6750:

```
WWW-Authenticate: Bearer realm="coredesign-identity", error="invalid_token"
```

The endpoint must support both `GET` and `POST` per the OIDC Core 1.0 spec (section 5.3).

**Definition of done:** UserInfo returns only claims for the scopes present in the access token. An expired or invalid token returns 401 with a correct `WWW-Authenticate` header. The Basic OP conformance profile passes at `certification.openid.net`.

---

### Milestone 6: End Session Endpoint

**Goal:** Implement compliant logout.

**What to build:**

`EndSessionEndpoint.cs` — `GET /connect/endsession`:

Required parameters: none (all optional per spec, but see validation below).

Optional parameters: `id_token_hint`, `post_logout_redirect_uri`, `state`, `client_id`.

Validation:

- If `post_logout_redirect_uri` is present and `id_token_hint` is present, validate that the redirect URI is in the client's `allowedPostLogoutRedirectUris`. If validation fails, ignore `post_logout_redirect_uri` and do not redirect (per spec).
- If `id_token_hint` is present, validate the token's signature and extract the `sub` for audit logging. Do not reject an expired hint (spec allows this).
- After logout processing, redirect to `post_logout_redirect_uri?state={state}` if provided and validated. Otherwise return 200.

Update discovery to add `end_session_endpoint`.

**Definition of done:** The end session endpoint redirects correctly when given a valid `id_token_hint` and a registered `post_logout_redirect_uri`. An unregistered redirect URI is silently ignored and no redirect occurs.

---

### Milestone 7: Refresh Tokens

**Goal:** Support long-lived sessions without re-authentication.

**What to build:**

`RefreshTokenRecord.cs`:

```csharp
public sealed class RefreshTokenRecord
{
    public string Token { get; init; } = string.Empty;
    public string Subject { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string Scope { get; init; } = string.Empty;
    public DateTime IssuedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public bool Revoked { get; set; }
}
```

`IRefreshTokenStore.cs` and `InMemoryRefreshTokenStore.cs`.

When the authorization code exchange succeeds and `offline_access` is in the scope, issue a refresh token alongside the access and ID tokens. The refresh token is an opaque random string (not a JWT), stored in the refresh token store.

Add `grant_type=refresh_token` handling to `/connect/token`:

1. Validate `client_id` and `refresh_token`.
2. Load the token from the store. Return `invalid_grant` if not found, expired, or revoked.
3. Validate that `client_id` matches the token's stored client.
4. Issue a new access token (and optionally a new ID token).
5. Rotate the refresh token (issue a new one, revoke the old one) per RFC 6819 best practices.

Update discovery to add `grant_types_supported: ["authorization_code", "refresh_token"]` and `scopes_supported` to include `offline_access`.

**Definition of done:** A refresh token is issued when `offline_access` scope is requested. The refresh token grant issues a new access token and rotates the refresh token. A revoked or expired refresh token returns `invalid_grant`.

---

### Milestone 8: Token Revocation and Introspection

**Goal:** Implement RFC 7009 (revocation) and RFC 7762 (introspection). These are used by resource servers and management tooling.

**What to build:**

`RevocationEndpoint.cs` — `POST /connect/revocation`:

- Accepts `token` and optional `token_type_hint` (`access_token` or `refresh_token`).
- Requires client authentication (at minimum, `client_id` for public clients).
- Always returns 200, even if the token was not found (per RFC 7009 section 2.2).
- If the token is a refresh token, marks it revoked in the refresh token store.
- If the token is an access token, adds it to an in-memory blocklist with its remaining TTL.

`IntrospectionEndpoint.cs` — `POST /connect/introspect`:

- Protected endpoint: requires `client_id` and (for confidential clients) `client_secret`.
- Returns an RFC 7762 response:

```json
{
  "active": true,
  "sub": "user-id",
  "client_id": "sampleapi-frontend",
  "scope": "openid profile email",
  "exp": 1234567890,
  "iat": 1234567000,
  "token_type": "Bearer"
}
```

- Returns `{ "active": false }` for any token that is expired, revoked, or not found.

Update discovery to add `revocation_endpoint` and `introspection_endpoint`.

**Definition of done:** Revocation marks a refresh token as revoked and subsequent refresh token grants with that token return `invalid_grant`. Introspection returns `active: false` for a revoked or expired token.

---

### Milestone 9: Dynamic Client Registration (RFC 7591)

**Goal:** Allow Relying Parties to register themselves at runtime without manually editing `clients.json`.

**What to build:**

`RegistrationEndpoint.cs` — `POST /connect/register`:

Request body (application/json): client metadata including `redirect_uris` (required), `grant_types`, `response_types`, `token_endpoint_auth_method`, `client_name`, `scope`.

Response (201 Created): the registered metadata plus `client_id`, `client_secret` (if confidential), `client_id_issued_at`, `client_secret_expires_at`, `registration_access_token`, and `registration_client_uri`.

`IClientRegistrationStore.cs` — extends `IClientStore` with a `RegisterAsync` method.

`InMemoryClientRegistrationStore.cs` — in-memory store for dynamically registered clients. Registered clients are not persisted between restarts. For production use, this would be backed by a database.

Protection: the registration endpoint is optionally protected by an initial access token (configured in `IdentityOptions`). If `InitialAccessToken` is configured, the `Authorization: Bearer {token}` header is required on registration requests.

Update discovery to add `registration_endpoint`.

**Definition of done:** A client POSTs its `redirect_uris` and `grant_types` to `/connect/register` and receives back a `client_id`. That `client_id` is immediately usable in the authorization flow. The Dynamic OP conformance profile passes at `certification.openid.net`.

---

### Milestone 10: Conformance Testing and Certification

**Goal:** Run the OpenID Conformance Suite against the complete server and submit for certification.

**Setup:**

The conformance suite at `certification.openid.net` requires a publicly reachable server. For local development use a tunneling tool (ngrok, Cloudflare Tunnel, or VS Dev Tunnels) to expose the local Identity API.

Create an account at `certification.openid.net` and configure a test plan:

- Server metadata URL: `https://{your-tunnel}/.well-known/openid-configuration`
- Client registration: use the dynamic registration endpoint
- Test profile: Basic OP, then Config OP, then Dynamic OP

**Running the suite:**

The suite issues automated requests across all required test scenarios. Each test returns a pass, warning, or failure result. Failures must be fixed before submission. Warnings may be acceptable depending on their category.

Common failure areas based on known implementation patterns:

- `iss` in the ID Token must exactly match the `issuer` in the discovery document (no trailing slash differences)
- The `nonce` claim must be included in the ID Token whenever it was present in the authorization request
- The `state` parameter must be returned unchanged in the redirect
- The `at_hash` computation must use the left half of the SHA-256 hash in base64url encoding without padding
- Error responses must use the exact `error` values defined in the spec (e.g., `invalid_grant`, not `invalid_credentials`)

**Submission:**

Once all targeted profile tests pass, submit the results via the certification portal. The submission includes the test logs generated by the conformance suite. After review (typically automated), the server appears on the OpenID Foundation certified implementations list.

**Definition of done:** All tests in the Basic OP, Config OP, and Dynamic OP profiles pass. The submission is accepted and a certified listing appears at `openid.net/developers/certified-openid-connect-implementations/`.

---

## 6. Discovery Document — Required Fields by Milestone

The table below tracks which discovery fields are introduced at each milestone. Fields marked as required by OpenID Connect Discovery 1.0 are noted.

| Field | Required | Introduced at milestone |
|---|---|---|
| `issuer` | Yes | Already present |
| `authorization_endpoint` | Yes | 4 |
| `token_endpoint` | Yes | Already present |
| `userinfo_endpoint` | Recommended | Already present |
| `jwks_uri` | Yes | Already present |
| `registration_endpoint` | Optional | 9 |
| `scopes_supported` | Recommended | Already present (updated at 7) |
| `response_types_supported` | Yes | Already present (updated at 4) |
| `response_modes_supported` | Optional | 4 (query only) |
| `grant_types_supported` | Optional | Already present (updated at 4, 7) |
| `acr_values_supported` | Optional | Not planned |
| `subject_types_supported` | Yes | Already present |
| `id_token_signing_alg_values_supported` | Yes | Already present |
| `userinfo_signing_alg_values_supported` | Optional | Not planned |
| `token_endpoint_auth_methods_supported` | Optional | 1 |
| `claims_supported` | Recommended | Already present |
| `code_challenge_methods_supported` | Optional | 4 |
| `end_session_endpoint` | Optional | 6 |
| `revocation_endpoint` | Optional | 8 |
| `introspection_endpoint` | Optional | 8 |
| `request_parameter_supported` | Optional | Not planned |
| `request_uri_parameter_supported` | Optional | Not planned |

---

## 7. Future Extensions (Post-Certification)

These features are not required for certification but are logical next steps for a production-grade server.

| Feature | Specification | Notes |
|---|---|---|
| Login UI | None (implementation-specific) | A hosted HTML login page to replace the HTTP Basic credential workaround on the authorization endpoint |
| Persistent client store | None | Back the client registration store with a database instead of in-memory |
| Persistent refresh token store | None | Required for refresh tokens to survive server restarts |
| Session management | OpenID Connect Session Management 1.0 | iframe-based, browser-only; rarely needed |
| Back-channel logout | OpenID Connect Back-Channel Logout 1.0 | Server-to-server logout notification |
| Request objects | RFC 9101 (JAR) | JWT-encoded authorization requests |
| Pushed Authorization Requests | RFC 9126 (PAR) | Pre-authorized authorization requests |
| Multiple signing keys with rotation | None | Key rotation without downtime |
| `acr` and `amr` claims | OIDC Core 1.0 | Authentication context and method references |
