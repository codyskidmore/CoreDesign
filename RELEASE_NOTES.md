# Release Notes

## CoreDesign Packages

### CoreDesign.Identity.Server 1.0.7

#### Authorization Code with PKCE (browser login flow)

The identity server now implements the full Authorization Code with PKCE flow, making it compatible with Blazor Server and other browser-based applications that use ASP.NET Core's built-in OIDC middleware.

New endpoints:

| Endpoint | Purpose |
| --- | --- |
| `GET /connect/authorize` | Renders the hosted login form |
| `POST /connect/authorize` | Processes login form submission and issues an authorization code |

The token endpoint (`POST /connect/token`) now also accepts the `authorization_code` grant type in addition to the existing `password` grant.

#### Client store

Browser-based login requires registered clients. A new `clients.json` file format and `IClientStore` interface allow the server to validate `client_id`, redirect URIs, allowed grant types, and PKCE requirements for each registered application. The built-in JSON file store is registered with `AddJsonFileClientStore`.

#### Standalone web host

A new hosting pattern, `AddIdentityServerWebHost` and `MapIdentityServerWebHost`, sets up the full identity server stack from a single `CoreDesign:IdentityWebHost` configuration section. It registers both JSON file stores, enables CORS, and serves a landing page at `/`. This is now the recommended approach for dedicated identity server projects.

#### Template customization

The login form, error banner, and landing page are now fully customizable without modifying the library. Place override files in an `identity-templates` folder at the project content root and they are picked up automatically. The embedded defaults serve as a starting point. The error banner is extracted into its own `login-error.html` template so the two concerns can be styled independently.

#### Bug fixes

- Fixed a 403 error that occurred during the browser-based authorization code exchange.
- Fixed the error alert box not rendering correctly after a failed login attempt.

### CoreDesign.Identity.Client 1.0.7

- The client now reads issuer and audience configuration from both `CoreDesign:Identity` and `CoreDesign:IdentityWebHost` sections, with `CoreDesign:IdentityWebHost` taking precedence. This makes the client and server configuration symmetric when using the standalone web host pattern.

### CoreDesign.Logging 1.0.3

New package.

`CoreDesign.Logging` provides a `DispatchProxy`-based logging middleware that automatically logs every method invocation, return value, and exception for any service registered through it. Service implementations require no changes.

Register a service with the proxy wrapper:

```csharp
services.AddWithLogging<IWeatherForecastService, WeatherForecastService>();
```

Sensitive data control:

- `[Redact]` on an interface method parameter replaces that argument with `"[REDACTED]"` in log output while passing the real value to the implementation.
- `[Suppress]` on an interface method skips all logging for that method.

Log levels follow the return type: informational for success, warning for `NotFoundMessage` or `BadRequestMessage` results, and error for exceptions.

### CoreDesign.Data 1.0.1

No new features in this release. Documentation has been updated to cover setup steps, entity configuration, and repository registration in more detail.

### CoreDesign.Shared 1.0.1

No changes.

## Sample Application

### Renamed from SampleApi to Sample

The sample solution and all project namespaces have been renamed from `SampleApi.*` to `Sample.*` to reflect the expanded scope of the project. The solution file is now `Sample/Sample.slnx`.

### New projects

**Sample.Identity.Web** is a dedicated standalone identity server using the new `AddIdentityServerWebHost` pattern. It hosts the login form for Blazor OIDC flows. Two accounts are pre-configured for local development:

| Email | Password | Roles |
| --- | --- | --- |
| admin@sampleapi.local | Password1! | DevAdmin, DevAppUsers |
| user@sampleapi.local | Password1! | DevAppUsers |

**Sample.Blazor** is a Blazor Server application that authenticates against Sample.Identity.Web using Authorization Code with PKCE and calls Sample.Api to display weather forecast data. The `Blazor:AuthProvider` configuration key selects between the local identity server and Azure Entra ID at startup.

### Other sample changes

- All services now run on fixed HTTPS ports to ensure the OIDC issuer URL remains stable across Aspire restarts.
- Client and identity store JSON files have been moved to a single `Shared/` folder so both identity server instances read from the same data.
- The Aspire AppHost has been refactored into `AppHostExtensions.cs` for readability.
- Fixed a Blazor page layout issue where the navigation sidebar overlapped the main content area.

## Tests

New test coverage added for `CoreDesign.Identity.Server`:

- `AuthorizeEndpointTests` covering the authorization code flow, PKCE validation, and error cases.
- `TokenEndpointTests` covering password grant, authorization code grant, and invalid request handling.
- `UserInfoEndpointTests` covering token validation and claim mapping.
