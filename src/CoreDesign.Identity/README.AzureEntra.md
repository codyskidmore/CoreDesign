# Azure Entra Authentication

This project uses `CoreDesign.Identity.Server` and `CoreDesign.Identity.Client` in the `Development` environment and switches to Azure Entra (formerly Azure Active Directory) in all other environments. The API code treats both as interchangeable JWT Bearer providers: the only differences are which authority signs the tokens and how permissions are assigned to users.

## Environment Summary

| Environment | Auth provider | Permissions |
| --- | --- | --- |
| `Development` | CoreDesign.Identity.Server (local) | Declared in `identities.json` as a `permissions` array |
| `AzureDev`, `UAT`, `Production` | Azure Entra | Entra App Roles with permission string values, mapped to `permissions` claims via claims transformation |

Authorization uses the same permission strings in every environment. Only the token issuer and the mechanism for assigning permissions to users differs.

## How the Switch Works

`AddIdentityAuthentication` selects the provider at startup:

```csharp
if (builder.Environment.IsDevelopment())
    builder.Services.AddIdentityClient(builder.Configuration);
else
    builder.AddAzureEntraAuthentication();
```

`AddAzureEntraAuthentication` configures JWT Bearer to trust tokens from Entra and validates issuer, audience, and lifetime. Because Entra emits App Role assignments as `roles` claims rather than `permissions` claims, a claims transformation step is required to bridge the two.

## Step 1: Create an App Registration

In the Azure portal, go to **Azure Active Directory > App registrations > New registration**.

| Field | Value |
| --- | --- |
| Name | Something descriptive, e.g. `CoreDesign API (UAT)` |
| Supported account types | Accounts in this organizational directory only |
| Redirect URI | Leave blank (this registration is for the API, not a client) |

After creation, note the **Application (client) ID** and the **Directory (tenant) ID** from the Overview page.

## Step 2: Expose an API

Under **Expose an API**, set the Application ID URI. Azure defaults this to `api://<client-id>`. This value becomes the `Audience` in the API's configuration and the `scope` prefix clients request.

Add a scope so client applications can request access:

| Field | Value |
| --- | --- |
| Scope name | `access_as_user` |
| Who can consent | Admins and users |
| Admin consent display name | Access CoreDesign API |

## Step 3: Define App Roles

Under **App roles**, create a role for each permission string the API uses. The **Value** field must exactly match the permission string declared in your application (e.g., in `Permissions.cs`). Use the same roles across all environments.

| Display name | Value | Allowed member types |
| --- | --- | --- |
| Weather Read | `weather:read` | Users/Groups |
| Weather Write | `weather:write` | Users/Groups |

Add one entry for each permission string your API defines. Values are case-sensitive and must match exactly.

## Step 4: Assign Users to Roles

In **Azure Active Directory > Enterprise applications**, find the app registration created above. Under **Users and groups**, assign each user or group to the appropriate roles. A user assigned to `weather:write` can call write endpoints; a user assigned only to `weather:read` cannot. Users with no role assignment receive no `roles` claim and are denied by the API's fallback authentication policy.

## Step 5: Configure the API

Replace the placeholder values in the appropriate environment appsettings file with the real tenant and client IDs from the App Registration overview page:

```json
{
  "AzureAd": {
    "Instance": "https://login.microsoftonline.com/",
    "TenantId": "<your-tenant-id>",
    "ClientId": "<your-client-id>",
    "Audience": "api://<your-client-id>"
  }
}
```

The API resolves the JWT authority as `{Instance}/{TenantId}/v2.0` and validates the `aud` claim against `Audience`.

Entra App Roles are emitted in tokens as `roles` claims. The `PermissionAuthorizationHandler` checks for `permissions` claims, so a claims transformation bridges the two. Register it alongside `AddAzureEntraAuthentication`:

```csharp
public class RolesToPermissionsTransformation : IClaimsTransformation
{
    public Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        var identity = (ClaimsIdentity)principal.Identity!;
        foreach (var role in principal.FindAll("roles").ToList())
            identity.AddClaim(new Claim("permissions", role.Value));
        return Task.FromResult(principal);
    }
}
```

Register the transformation in the service container (typically inside `AddAzureEntraAuthentication`):

```csharp
services.AddScoped<IClaimsTransformation, RolesToPermissionsTransformation>();
```

With this in place, each Entra `roles` claim value (which equals the App Role `Value` field) is copied to a `permissions` claim, and `RequireAuthorization("weather:read")` works identically in both development and production.

## Step 6: Register a Client Application

Any application that calls the API needs its own App Registration. In the client's registration:

1. Under **Authentication**, add the appropriate platform and redirect URIs for the client type.
2. Under **API permissions**, add a permission to the API registration created in Step 1 and select the `access_as_user` scope (or `/.default` for client credentials).
3. Grant admin consent if the scope requires it.

The client then requests a token from:

```
POST https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token
```

with the scope set to `api://{api-client-id}/access_as_user`. The resulting access token is sent as `Authorization: Bearer <token>` to the API.

## Claim Mapping

The API configures JWT Bearer with `MapInboundClaims = false`. Entra tokens use standard claim names:

| Claim in token | Used as | Note |
| --- | --- | --- |
| `roles` | App Role assignments | Mapped to `permissions` claims via `RolesToPermissionsTransformation` before authorization runs |
| `oid` | Object ID | Present by default |

If users need the `email` claim populated, ensure **Optional claims** includes `email` in the token configuration for the API's App Registration (under **Token configuration > Add optional claim > Access token > email**).

## Troubleshooting

**401 on all requests**: Verify `AzureAd:TenantId` and `AzureAd:Audience` are set correctly. The audience in the token (`aud` claim) must exactly match the configured value, including the `api://` prefix.

**403 on protected endpoints**: The user's token contains no matching `permissions` claim after transformation. Check that `RolesToPermissionsTransformation` is registered, that the user is assigned to the correct App Role in the Enterprise application, and that the App Role `Value` field exactly matches the permission string used in `RequireAuthorization()`.

**IDX20804 / metadata failure**: The API could not reach the Entra metadata endpoint at startup. Check outbound internet connectivity and confirm `AzureAd:Instance` and `AzureAd:TenantId` form a valid authority URL.
