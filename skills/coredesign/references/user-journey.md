# CoreDesign.UserJourney

Per-user activity tracking on OpenTelemetry: every request, page view, click and data change linked to the signed-in user, in Application Insights or any OpenTelemetry backend. Three packages:

| Package | Where | Use it for |
|---|---|---|
| `CoreDesign.UserJourney` | Any ASP.NET Core app or API | User and IP tags on requests, `Journey.Viewed`, the browser events endpoint, health-probe and query-string filtering |
| `CoreDesign.UserJourney.Blazor` | Blazor Server apps (includes the core package) | Blazor clicks and navigation, which travel over SignalR, linked to the user; `JourneyView` component |
| `@coredesign.dev/user-journey` (npm) | React and other single-page apps | Page views and `data-journey` clicks sent to the API |

## Contents

1. Registration order (the one thing to get right)
2. API or MVC app
3. Blazor Server app
4. React or other single-page app
5. Recording what a page showed
6. Options
7. Tags and the workbook
8. Troubleshooting

## 1. Registration order

`AddUserJourney()` and `AddUserJourneyBlazor()` must be called **before** `AddServiceDefaults()`, `AddAspireServiceDefaults()` or `UseAzureMonitor()`. OpenTelemetry runs processors in registration order, and the Blazor package adds the user to a trace when it ends; registered after the exporter, the trace is sent before the user is added. `UseUserJourney()` must come **after** `UseAuthentication()` and `UseAuthorization()`, so `HttpContext.User` is known.

## 2. API or MVC app

```csharp
builder.Services.AddUserJourney();
builder.AddServiceDefaults();
// ...
app.UseAuthentication();
app.UseAuthorization();
app.UseUserJourney();
app.MapUserJourneyEvents();   // only if a browser app sends page views and clicks
```

With Application Insights, keep every trace so timelines have no gaps:

```csharp
builder.Services.AddOpenTelemetry().UseAzureMonitor(o =>
{
    o.ConnectionString = connectionString;
    o.SamplingRatio = 1.0F;
});
```

Data changes need no code: POST, PUT, PATCH and DELETE requests to the API, and outgoing calls from a Blazor app, share the trace with the user's page or click.

## 3. Blazor Server app

```csharp
builder.Services.AddUserJourneyBlazor();   // replaces AddUserJourney; same options
builder.AddServiceDefaults();
// ...
app.UseAuthentication();
app.UseAuthorization();
app.UseUserJourney();
```

The package maps each circuit to the signed-in user when it opens and adds `enduser.id` to Blazor event traces (`Event ...`) through the `aspnetcore.components.circuit.id` tag. Static server-rendered pages and enhanced navigation are ordinary HTTP requests and are tagged by `UseUserJourney`. Blazor WebAssembly runs in the browser; treat it like a single-page app (section 4).

## 4. React or other single-page app

API side: `AddUserJourney()`, `UseUserJourney()` and `MapUserJourneyEvents()` (section 2). The endpoint (default `POST /user-journey/events`) requires a signed-in user and accepts up to 50 events per request.

Front end:

```bash
npm install @coredesign.dev/user-journey
```

In PowerShell, put the package name in quotes, because PowerShell treats a leading `@` as an operator:

```powershell
npm install "@coredesign.dev/user-journey"
```

```tsx
import { UserJourneyProvider, useJourneyPage } from '@coredesign.dev/user-journey/react'

// Inside the sign-in provider, so the access token is available.
function JourneyProvider({ children }: { children: React.ReactNode }) {
  const auth = useAuth()
  return (
    <UserJourneyProvider options={{
      endpoint: '/api/user-journey/events',            // through the dev proxy, or the API's full URL
      getAccessToken: () => auth.user?.access_token,
    }}>
      {children}
    </UserJourneyProvider>
  )
}

// Page views: pass the router's path, or a name for state-based navigation.
useJourneyPage(useLocation().pathname)
useJourneyPage(`/products/${product.id}`, { type: 'Product', name: product.name })

// Clicks: name the element. Clicks anywhere inside it count.
<button data-journey="add-to-cart">Add to cart</button>
```

If the API is on another origin, add the app's origin to the API's CORS policy. Without React, use `createUserJourney({ endpoint, getAccessToken })`, then `start()`, `page(path)` and `click(name)`. Nothing is sent while `getAccessToken` returns no token. For apps without a router, pass `getPath` so clicks record the current page instead of `/`.

## 5. Recording what a page showed

```csharp
Journey.Viewed("Product", product.Name);      // in a handler, endpoint or page
```

```razor
<JourneyView Type="Product" Item="@product.Name" />
```

It tags the current trace with `view.type` and `view.item`; the workbook's "Items viewed" report groups by them. Keep type names in constants. Never pass personal data or text the user typed.

## 6. Options

```csharp
builder.Services.AddUserJourney(o =>
{
    o.ClientIpHeaders = ["CF-Connecting-IP"];   // real client IP behind Cloudflare
    o.ClientIpTag = "client_ip_actual";         // keep an existing query's dimension name
    o.CaptureUserEmail = false;
    o.ExcludedPaths.Add("/ready");
});
```

| Option | Default |
|---|---|
| `UserIdClaimTypes` | `oid`, `http://schemas.microsoft.com/identity/claims/objectidentifier`, `sub`, `ClaimTypes.NameIdentifier` |
| `UserEmailClaimTypes` | `preferred_username`, `email`, `ClaimTypes.Email` |
| `UserNameClaimTypes` | `name`, `ClaimTypes.Name` |
| `CaptureUserEmail`, `CaptureUserName`, `CaptureClientIp` | `true` |
| `ClientIpHeaders` | empty: use the connection address (after `UseForwardedHeaders`) |
| `ClientIpTag` | `user_journey.client_ip` |
| `ExcludedPaths` | `/alive`, `/health` (prefix match) |
| `RemoveQueryStrings` | `true` |
| `BrowserEventsPath` | `/user-journey/events` |
| `MaxBrowserEventsPerBatch` | `50` |

Only list IP headers your own proxy sets and overwrites; a client can send any header.

## 7. Tags and the workbook

Tags: `enduser.id` (Application Insights shows it as `user_AuthenticatedId`), `user.email`, `user.name`, the client IP tag, `view.type`, `view.item`; browser events add `user_journey.event`, `user_journey.path` and `user_journey.target` and are named `Page {path}` or `Click {target}` on the `CoreDesign.UserJourney` ActivitySource.

The package ships `workbooks/user-journey.workbook.json`. Import it in the Azure portal: Application Insights resource, Workbooks, New, Advanced Editor (`</>`), Gallery Template, paste, Apply, Save. It has a per-user timeline, signed-in users per day, most viewed pages, items viewed and common data changes, with an App picker for shared resources.

## 8. Troubleshooting

| Symptom | Cause |
|---|---|
| Requests have no `enduser.id` | `UseUserJourney()` placed before authentication, or the user ID claim has a name not in `UserIdClaimTypes` |
| Blazor clicks have no user | `AddUserJourneyBlazor()` registered after the service defaults or Azure Monitor |
| Browser events return 401 | No or expired access token; the endpoint requires a signed-in user |
| Browser events return 400 | Unknown `kind`, a path not starting with `/`, a click without `target`, or more than `MaxBrowserEventsPerBatch` events |
| Gaps in a user's timeline | Sampling; set `SamplingRatio = 1.0F` for Application Insights |
| Health probes fill the telemetry | The probe path is not in `ExcludedPaths` |
