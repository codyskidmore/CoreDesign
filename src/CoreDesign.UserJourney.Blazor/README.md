# CoreDesign.UserJourney.Blazor

Blazor Server support for [CoreDesign.UserJourney](https://www.nuget.org/packages/CoreDesign.UserJourney). Blazor clicks and in-app navigation travel over the SignalR circuit instead of HTTP, so request middleware never sees them. This package remembers which signed-in user owns each circuit and adds that user to the traces ASP.NET Core writes for Blazor events, so they appear in the user's journey next to their page views and data changes.

## Installation

```bash
dotnet add package CoreDesign.UserJourney.Blazor
```

It includes `CoreDesign.UserJourney`.

## Setup

```csharp
// Before AddServiceDefaults() or UseAzureMonitor(): the user is added when a Blazor trace ends,
// so this must run before the exporter sends it.
builder.Services.AddUserJourneyBlazor();
builder.AddServiceDefaults();

var app = builder.Build();
app.UseAuthentication();
app.UseAuthorization();
app.UseUserJourney();
```

`AddUserJourneyBlazor` accepts the same options as `AddUserJourney`. It also adds the `Microsoft.AspNetCore.Components` ActivitySources to tracing, so Blazor event and navigation traces are collected.

## Recording what a page showed

```razor
<JourneyView Type="Product" Item="@product.Name" />
```

`JourneyView` renders nothing. It tags the current trace with `view.type` and `view.item`, which the workbook's "Items viewed" report groups by. Never pass personal data.

## Notes

- Blazor Server only. Blazor WebAssembly runs in the browser; use the npm package `@coredesign.dev/user-journey` and `MapUserJourneyEvents` for it.
- Circuits opened before sign-in are not linked; the user's next page load opens a new, linked circuit.
- If clicks show no user, check that `AddUserJourneyBlazor` is called before the service defaults or `UseAzureMonitor`.
