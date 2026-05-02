# Azure Entra Authentication

This project uses `CoreDesign.Identity.Server` and `CoreDesign.Identity.Client` in the `Development` environment and switches to Azure Entra (formerly Azure Active Directory) in all other environments. The API code treats both as interchangeable JWT Bearer providers: the only differences are which authority signs the tokens and which role names are expected.

## Environment summary

| Environment | Auth provider | Role names |
| --- | --- | --- |
| `Development` | CoreDesign.Identity.Server (local) | `DevAdmin`, `DevAppUsers` |
| `AzureDev` | Azure Entra | `DevAdmin`, `DevAppUsers` |
| `UAT` | Azure Entra | `UATAdmin`, `UATUsers` |
| `Production` | Azure Entra | `AdminUsers`, `AppUsers` |

## How the switch works

`AddIdentityAuthentication` in `Dcne.Api` selects the provider at startup:

```csharp
if (builder.Environment.IsDevelopment())
    builder.Services.AddIdentityClient(builder.Configuration);
else
    builder.AddAzureEntraAuthentication();
```

`AddAzureEntraAuthentication` configures JWT Bearer to trust tokens from Entra and validates issuer, audience, and lifetime. Role and name claim types are kept identical to the local setup so authorization policies and endpoint code require no changes between environments.

## Step 1: Create an App Registration

In the Azure portal, go to **Azure Active Directory > App registrations > New registration**.

| Field | Value |
| --- | --- |
| Name | Something descriptive, e.g. `CoreDesign API (UAT)` |
| Supported account types | Accounts in this organizational directory only |
| Redirect URI | Leave blank (this registration is for the API, not a client) |

After creation, note the **Application (client) ID** and the **Directory (tenant) ID** from the Overview page. These populate the config values below.

## Step 2: Expose an API

Under **Expose an API**, set the Application ID URI. Azure defaults this to `api://<client-id>`. This value becomes the `Audience` in the API's configuration and the `scope` prefix clients request.

Add a scope so client applications can request access:

| Field | Value |
| --- | --- |
| Scope name | `access_as_user` |
| Who can consent | Admins and users |
| Admin consent display name | Access CoreDesign API |

## Step 3: Define App Roles

Under **App roles**, create a role for each role name the API expects for the target environment. Role names must match exactly what the authorization policies check.

**AzureDev**

| Display name | Value | Allowed member types |
| --- | --- | --- |
| Dev Administrator | `DevAdmin` | Users/Groups |
| Dev App Users | `DevAppUsers` | Users/Groups |

**UAT**

| Display name | Value | Allowed member types |
| --- | --- | --- |
| UAT Administrator | `UATAdmin` | Users/Groups |
| UAT App Users | `UATUsers` | Users/Groups |

**Production**

| Display name | Value | Allowed member types |
| --- | --- | --- |
| Administrator | `AdminUsers` | Users/Groups |
| App Users | `AppUsers` | Users/Groups |

## Step 4: Assign users to roles

In **Azure Active Directory > Enterprise applications**, find the app registration created above. Under **Users and groups**, assign each user or group to the appropriate role. Users without a role assignment will receive tokens with no `roles` claim and will be denied by the API's authorization policies.

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

The API resolves the JWT authority as `{Instance}/{TenantId}/v2.0` and validates the `aud` claim against `Audience`. No other configuration is needed on the API side.

## Step 6: Register a client application

Any application that calls the API needs its own App Registration. In the client's registration:

1. Under **Authentication**, add the appropriate platform and redirect URIs for the client type.
2. Under **API permissions**, add a permission to the API registration created in Step 1 and select the `access_as_user` scope (or `/.default` for client credentials).
3. Grant admin consent if the scope requires it.

The client then requests a token from:

```
POST https://login.microsoftonline.com/{tenant-id}/oauth2/v2.0/token
```

with the scope set to `api://{api-client-id}/access_as_user`. The resulting access token is sent as `Authorization: Bearer <token>` to the API.

## Claim mapping

The API configures JWT Bearer with `MapInboundClaims = false`. Entra tokens use standard claim names, but some differ from the defaults Microsoft's middleware applies. The relevant mappings:

| Claim in token | Used as | Note |
| --- | --- | --- |
| `roles` | Role claim | Populated from App Role assignments |
| `preferred_username` or `upn` | Not mapped to name | `NameClaimType` is set to `email` |
| `oid` | Object ID | Present by default |

If users need the `email` claim populated, ensure **Optional claims** includes `email` in the token configuration for the API's App Registration (under **Token configuration > Add optional claim > Access token > email**).

## Troubleshooting

**401 on all requests**: Verify `AzureAd:TenantId` and `AzureAd:Audience` are set correctly. The audience in the token (`aud` claim) must exactly match the configured value, including the `api://` prefix.

**403 on protected endpoints**: The user's token contains no `roles` claim or the role value does not match the expected name for the current environment. Check the App Role assignment in the Enterprise application and confirm the role `Value` field (not Display name) matches the string in `AuthorizationRoles.cs`.

**IDX20804 / metadata failure**: The API could not reach the Entra metadata endpoint at startup. Check outbound internet connectivity and confirm `AzureAd:Instance` and `AzureAd:TenantId` form a valid authority URL.
