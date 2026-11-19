# @coredesign.dev/user-journey

Browser page views and clicks for [CoreDesign.UserJourney](https://www.nuget.org/packages/CoreDesign.UserJourney). Your single-page app sends the signed-in user's activity to your ASP.NET Core API, where it joins the same per-user journey as the API's own requests and data changes. No dependencies; React support included; about 4 kB gzipped.

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

## Not missing clicks

A click on an element without `data-journey` is not recorded, so a forgotten attribute silently loses that click. Page views and data changes are still recorded either way. Three things close the gap; use all three.

### 1. Record unnamed clicks anyway

```tsx
<UserJourneyProvider options={{
  endpoint: '/api/user-journey/events',
  getAccessToken: () => auth.user?.access_token,
  autoClickNames: true,
  warnUnnamed: import.meta.env.DEV,
}}>
```

With `autoClickNames`, a click on a button, link, `summary`, button-like `input` or element with a role such as `button`, `link`, `tab` or `menuitem` is recorded even without `data-journey`:

| Element | Recorded as |
|---|---|
| `<button data-journey="save-order">` | `save-order` (an explicit name always wins) |
| `<button data-testid="save">` | `save` |
| `<button aria-label="Close dialog">` | `Close dialog` |
| `<input type="submit" name="confirm">` | `confirm` |
| `<a href="/help" id="help-link">` | `help-link` |
| `<button>Export 12 orders</button>` | `(unnamed button)` |

The attributes are tried in that order: `data-testid`, `aria-label`, `name`, `id`. Pass your own list instead of `true` to change it, for example `autoClickNames: ['data-testid']`. IDs that React generates with `useId` are skipped. Element text is never used, because it can contain data. Only list attributes whose values never contain personal data: an `aria-label` such as "Delete Jane's order" would be recorded as written.

`(unnamed button)` clicks appear in the workbook's timeline and in its **Clicks without a name** report, grouped by page, so missed names are visible and easy to fix.

Mark an element, or a whole area, with `data-journey-ignore` to leave it out (a carousel's arrows, say). Explicit `data-journey` names inside it are still recorded.

A `<div onClick>` cannot be detected, because React attaches handlers at the root. Give it `role="button"`, which accessibility lint rules ask for anyway.

### 2. Warn while developing

`warnUnnamed: import.meta.env.DEV` logs a console warning the first time each unnamed element is clicked, with its opening tag, the page and what was recorded. Keep it off in production.

### 3. Fail tests when a name is missing

```tsx
import { render } from '@testing-library/react'
import { assertJourneyNames } from '@coredesign.dev/user-journey/testing'

it('names every clickable element', () => {
  const { container } = render(<OrdersPage />)
  assertJourneyNames(container)
})
```

`assertJourneyNames` throws, listing each clickable element without `data-journey` (or a named container, or `data-journey-ignore`). It works with any test runner. Pass `{ autoClickNames: true }` to accept elements the tracker can name from their attributes; leave it off to require explicit names, which stay stable when test IDs and labels change. `findUnnamedClickables(root)` returns the elements instead of throwing.

### Making names impossible to forget

If your app has a shared `Button` component, give it a required `journey` prop that renders `data-journey`. TypeScript then rejects a button without a name, and a redesign cannot drop it:

```tsx
type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & { journey: string }

export function Button({ journey, ...props }: ButtonProps) {
  return <button data-journey={journey} {...props} />
}
```

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
| `autoClickNames` | off | `true`, or a list of attributes: also record clickable elements without `data-journey` |
| `ignoreAttribute` | `data-journey-ignore` | Attribute that leaves an element and its children out of auto-naming and warnings |
| `warnUnnamed` | `false` | Console warning for each unnamed element clicked; for development |
| `getPath` | `location.pathname` | Page path recorded with clicks |

Events are queued and sent in batches, and once more with `keepalive` when the page is hidden. Sending never throws; tracking can't break your app.

## Privacy

Only paths (never query strings or fragments) and the names in `data-journey` attributes are sent; with `autoClickNames`, so are the values of the attributes you list. Form values and element text are never read. Nothing is sent unless the user is signed in.
