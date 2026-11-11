# CoreDesign.UserJourney

Follow what each signed-in user viewed and did in your ASP.NET Core app: every page view, click and data change, linked to the user, in Application Insights or any OpenTelemetry backend.

It does not add a tracking system. It enriches the telemetry your app already sends: each request is tagged with the signed-in user, browser apps send page views and clicks to a small endpoint, and Blazor Server clicks are linked through `CoreDesign.UserJourney.Blazor`. A ready-made Application Insights workbook shows a timeline for any user.

## Requirements

- .NET 11 and ASP.NET Core
- OpenTelemetry tracing with an exporter (Azure Monitor, OTLP, or any other)

## Installation

```bash
dotnet add package CoreDesign.UserJourney
```

For Blazor Server apps, install `CoreDesign.UserJourney.Blazor` instead; it includes this package. For React and other single-page apps, add the npm package `@coredesign.dev/user-journey` to the front end.

## Setup

```csharp
// 1. Before AddServiceDefaults() or UseAzureMonitor(), so it runs before their exporters.
builder.Services.AddUserJourney();
builder.AddServiceDefaults();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();

// 2. After authentication and authorization, so the signed-in user is known.
app.UseUserJourney();

// 3. Only when a browser app (React, Angular, Vue) sends page views and clicks.
app.MapUserJourneyEvents();
```

For Application Insights, keep every trace so timelines have no gaps:

```csharp
builder.Services.AddOpenTelemetry().UseAzureMonitor(o => o.SamplingRatio = 1.0F);
```

## What gets recorded

| Activity | How | Code needed |
|---|---|---|
| Page views | Request telemetry, tagged with the user | None |
| Data changes | POST, PUT, PATCH and DELETE requests and outgoing calls in the same trace | None |
| Items a page showed | `Journey.Viewed("Product", product.Name)` | One line per page |
| Browser page views and clicks | `@coredesign.dev/user-journey` posts them to `MapUserJourneyEvents` | Provider plus `data-journey` attributes |
| Blazor clicks and navigation | `CoreDesign.UserJourney.Blazor` | None beyond registration |

Tags written: `enduser.id` (Application Insights shows it as `user_AuthenticatedId`), `user.email`, `user.name`, `user_journey.client_ip`, `view.type` and `view.item`.

## Options

```csharp
builder.Services.AddUserJourney(o =>
{
    o.ClientIpHeaders = ["CF-Connecting-IP"];   // behind Cloudflare
    o.CaptureUserEmail = false;                 // keep personal data out of telemetry
    o.ExcludedPaths.Add("/ready");
});
```

| Option | Default | Purpose |
|---|---|---|
| `UserIdClaimTypes` | `oid`, Entra's long `oid` form, `sub`, `NameIdentifier` | Where the user ID comes from |
| `UserEmailClaimTypes`, `UserNameClaimTypes` | `preferred_username`, `email`; `name` | Email and display name claims |
| `CaptureUserEmail`, `CaptureUserName`, `CaptureClientIp` | `true` | Turn each personal field off |
| `ClientIpHeaders` | none | Headers holding the real client IP; only list headers your proxy overwrites |
| `ClientIpTag` | `user_journey.client_ip` | Tag name for the IP |
| `ExcludedPaths` | `/alive`, `/health` | Path prefixes whose telemetry is dropped |
| `RemoveQueryStrings` | `true` | Strip query strings from recorded URLs |
| `BrowserEventsPath` | `/user-journey/events` | Endpoint path for browser apps |
| `MaxBrowserEventsPerBatch` | `50` | Largest accepted browser batch |

## Browser events endpoint

`MapUserJourneyEvents()` maps `POST /user-journey/events`. It requires a signed-in user and accepts a JSON array:

```json
[
  { "kind": "page", "path": "/products/42", "itemType": "Product", "item": "Sprocket" },
  { "kind": "click", "path": "/products/42", "target": "add-to-cart" }
]
```

Each event is recorded on the `CoreDesign.UserJourney` ActivitySource as a child of the request, tagged with the user. Query strings and fragments are removed from paths. Invalid batches are rejected whole with 400.

## Privacy

- Only signed-in users get a journey. Anonymous traffic belongs in an aggregate analytics tool.
- Query strings are removed from URLs, and browser events carry paths and element names only. Form values, typed text and element text are never recorded.
- Email, name and IP can each be turned off. Tell users about per-user activity history in your privacy policy.

## The workbook

The package includes `workbooks/user-journey.workbook.json`, also in the CoreDesign repository. In the Azure portal, open your Application Insights resource, then **Workbooks**, **New**, **Advanced Editor** (the `</>` button), choose **Gallery Template**, paste the file, **Apply**, and **Save**. It shows a timeline per user, signed-in users per day, most viewed pages, items viewed and the most common data changes, with an App picker for multi-app resources.

## Feedback

Issues and suggestions: [github.com/codyskidmore/CoreDesign/issues](https://github.com/codyskidmore/CoreDesign/issues).
