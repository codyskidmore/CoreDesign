# @coredesign.dev/user-journey

Browser page views and clicks for [CoreDesign.UserJourney](https://www.nuget.org/packages/CoreDesign.UserJourney). Your single-page app sends the signed-in user's activity to your ASP.NET Core API, where it joins the same per-user journey as the API's own requests and data changes. No dependencies; React support included; about 4 kB.

## The API side

```csharp
builder.Services.AddUserJourney();      // before AddServiceDefaults() / UseAzureMonitor()
// ...
app.UseAuthentication();
app.UseAuthorization();
app.UseUserJourney();
app.MapUserJourneyEvents();             // POST /user-journey/events, signed-in users only
```

## Install

```bash
npm install @coredesign.dev/user-journey
```

In PowerShell, put the package name in quotes, because PowerShell treats a leading `@` as an operator:

```powershell
npm install "@coredesign.dev/user-journey"
```

## React

Wrap your app inside your sign-in provider, so the tracker can read the access token:

```tsx
import { UserJourneyProvider } from '@coredesign.dev/user-journey/react'
import { useAuth } from 'react-oidc-context'

function JourneyProvider({ children }: { children: React.ReactNode }) {
  const auth = useAuth()
  return (
    <UserJourneyProvider options={{
      endpoint: '/api/user-journey/events',
      getAccessToken: () => auth.user?.access_token,
    }}>
      {children}
    </UserJourneyProvider>
  )
}
```

Record page views by passing the current path to `useJourneyPage`:

```tsx
import { useLocation } from 'react-router'
import { useJourneyPage } from '@coredesign.dev/user-journey/react'

function Routes() {
  useJourneyPage(useLocation().pathname)
  // ...
}

function ProductPage({ product }) {
  useJourneyPage(`/products/${product.id}`, { type: 'Product', name: product.name })
  // ...
}
```

Record clicks by naming elements. Clicks anywhere inside a named element count:

```tsx
<button data-journey="add-to-cart" onClick={addToCart}>Add to cart</button>
```

For anything else, `useUserJourney()` returns the tracker: `tracker.click('export-csv')`.

## Without React

```ts
import { createUserJourney } from '@coredesign.dev/user-journey'

const journey = createUserJourney({
  endpoint: '/api/user-journey/events',
  getAccessToken: () => getToken(),
})
journey.start()
journey.page(location.pathname)
```

## Options

| Option | Default | Purpose |
|---|---|---|
| `endpoint` | required | URL of `MapUserJourneyEvents` |
| `getAccessToken` | required | Returns the current access token; nothing is sent while it returns none |
| `flushIntervalMs` | `5000` | How often queued events are sent |
| `maxBatchSize` | `20` | Events per request (the API accepts up to 50) |
| `captureClicks` | `true` | Record clicks on `data-journey` elements |
| `clickAttribute` | `data-journey` | Attribute that names clickable elements |
| `getPath` | `location.pathname` | Page path recorded with clicks |

Events are queued and sent in batches, and once more with `keepalive` when the page is hidden. Sending never throws; tracking can't break your app.

## Privacy

Only paths (never query strings or fragments) and the names in `data-journey` attributes are sent. Form values and element text are never read. Nothing is sent unless the user is signed in.
