# Proposal: Not Missing Clicks in React User Journeys

| | |
|---|---|
| Package | `@coredesign.dev/user-journey` (npm), plus the `CoreDesign.UserJourney` workbook |
| Origin | TMS change request, CR-5 ("Automatic click names") |
| Branch | `user-journey-workbook-fix` |
| Status | Implemented, awaiting review and testing |
| Compatibility | Opt-in. With default options the package records exactly what 11.0.1 records |

## 1. Problem

The browser package records a click only when the clicked element, or one of its ancestors, has a `data-journey` attribute. Everything else is dropped without a trace.

That makes click tracking depend on every developer remembering an attribute on every interactive element, forever:

* A developer adds a new button and forgets the attribute.
* A screen is redesigned, a component is replaced, and the attribute is not carried over.
* A third-party component renders its own buttons that nobody can annotate.

In each case the failure is silent. The app works, the tests pass, and the workbook simply shows fewer clicks than happened. Nobody can tell the difference between "users never click this" and "this is not tracked". TMS named about 25 buttons by hand and expects new ones to be missed.

### What is not at risk

The gap is narrower than it looks, which shapes the design:

| Activity | Depends on `data-journey`? |
|---|---|
| Page views | No. One `useJourneyPage(useLocation().pathname)` call in the layout covers every route, including new ones |
| Data changes | No. POST, PUT, PATCH and DELETE requests are recorded by the API |
| Items viewed | No. Recorded by `useJourneyPage(path, item)` or `Journey.Viewed` |
| Clicks that change nothing | **Yes** |

So a missed attribute loses the click itself, for example "opened the export dialog" or "switched to the Details tab". The timeline still shows the page and any data the click changed. The main harm is that the loss is invisible.

## 2. Goals

1. A missed name should be **visible**, not silent.
2. Missed names should be **caught before release**, in development and in CI.
3. Teams that want it should be able to make a missing name **impossible**.
4. **No new privacy exposure.** Element text stays unread, and nothing changes for apps that do not opt in.
5. **No breaking change** in an 11.x minor release.

### Non-goals

* Recording every DOM click (clicks on plain text, layout containers and so on). Only interactive elements matter for a journey.
* Inferring names from element text, CSS classes or DOM position. These either leak data or are unstable.
* Detecting React `onClick` handlers on non-interactive elements. React attaches all handlers at the root, so the DOM does not reveal them.

## 3. Design

Three layers, each closing the gap at a different point, plus a recommended application pattern.

| Layer | Where it acts | What it does |
|---|---|---|
| 1. `autoClickNames` | Production | Records unnamed clicks instead of dropping them, so gaps appear in the workbook |
| 2. `warnUnnamed` | Development | Console warning the first time each unnamed element is clicked |
| 3. `assertJourneyNames` | CI | Fails a component test when a clickable element has no name |
| 4. Required `journey` prop | Compile time | Application pattern: TypeScript rejects an unnamed shared `Button` |

### 3.1 What counts as clickable

One selector, shared by the tracker and the test helper so both agree exactly:

* `button`, `a[href]`, `summary`
* `input` of type `button`, `submit`, `reset`, `checkbox` or `radio`
* Any element with `role` set to `button`, `link`, `tab`, `menuitem`, `menuitemcheckbox`, `menuitemradio`, `option`, `switch`, `checkbox` or `radio`

A `<div onClick>` without a role is not clickable by this definition. Accessibility lint rules (`jsx-a11y/click-events-have-key-events`, `no-static-element-interactions`) already require a role on such elements, so the guidance is the same either way: give it `role="button"`.

### 3.2 How a click is named

For each click, in this order:

1. **Explicit name.** If the clicked element or an ancestor has a non-empty `data-journey`, record that name. This is the 11.0.1 rule, unchanged, and it always wins. Existing names never change.
2. **Not clickable.** If no clickable element contains the click target, record nothing.
3. **Ignored.** If the clickable element is inside an element with `data-journey-ignore`, record nothing.
4. **Auto-naming off** (the default). Record nothing. This is the 11.0.1 behaviour.
5. **Auto name.** Use the first non-empty value among the configured attributes. The default order is `data-testid`, `aria-label`, `name`, `id`.
6. **Unnamed.** Otherwise record `(unnamed <role or tag>)`: `(unnamed button)`, `(unnamed link)`, `(unnamed tab)`, `(unnamed checkbox)`.

Rules applied along the way:

* **IDs React generates are skipped.** `useId` produces `:r1:` (React 18), `«r1»` (19.0 and 19.1) and `_r_1_` or `_R_1_` (19.2 and later; verified against React 19.3.1). These change between renders and builds and mean nothing in a report.
* **Element text is never read**, at any step.
* **An explicit name inside an ignored area is still recorded.** `data-journey-ignore` only suppresses automatic behaviour; it never overrides a deliberate name.
* **An empty `data-journey`** (for example `data-journey=" "`) counts as missing and falls through to auto-naming.

#### Why this attribute order

| Attribute | Why it is there | Risk |
|---|---|---|
| `data-testid` | Stable, chosen by developers, already present in many codebases, never shown to users | Low |
| `aria-label` | Usually present on icon-only buttons, which are otherwise the hardest to identify | Can be templated with data ("Delete Jane's order") |
| `name` | Common on form buttons | Low |
| `id` | Often stable and descriptive | Can embed record IDs (`row-1234-edit`); generated React IDs are filtered |

Teams can replace the list, for example `autoClickNames: ['data-testid']` for the strictest setting.

### 3.3 Making gaps visible: `(unnamed ...)`

Recording `(unnamed button)` is deliberate. It looks less tidy than dropping the click, but it is the only option that tells anyone a name is missing. Without it, an auto-naming tracker would still lose every element that has no test ID or label, just as silently as before.

The workbook gets a new report, **Clicks without a name (browser apps)**, listing `(unnamed ...)` clicks by page with counts, distinct users and the last time they happened. Each row is a concrete task: name the element on that page, or mark it ignored. The timeline already shows these clicks as "Interacted: (unnamed button) on /orders".

### 3.4 Development warning: `warnUnnamed`

When on, the first click on each unnamed clickable element logs:

```text
[user-journey] <button class="primary"> on /orders has no data-journey attribute. The click was not recorded.
Add data-journey="name" to record it by name, or data-journey-ignore to skip it.
```

With `autoClickNames` on, the second sentence becomes `It was recorded as "save".`

* Warned once per element (a `WeakSet`), so repeated clicks do not flood the console, and nothing leaks when elements unmount.
* Not shown for named or ignored elements.
* Intended for development only: `warnUnnamed: import.meta.env.DEV`. The opening tag can contain attribute values, which is fine in a developer's console but is never sent anywhere.

### 3.5 Test helper: `@coredesign.dev/user-journey/testing`

A new entry point, so test code never ships in the app bundle:

```ts
import { assertJourneyNames, findUnnamedClickables } from '@coredesign.dev/user-journey/testing'

it('names every clickable element', () => {
  const { container } = render(<OrdersPage />)
  assertJourneyNames(container)
})
```

* `assertJourneyNames(root, options?)` throws a plain `Error` listing the opening tag of each unnamed element (up to 20, then a count). It needs no `expect`, so it works with Vitest, Jest, Playwright component tests or a real browser.
* `findUnnamedClickables(root, options?)` returns the elements, for custom assertions or reporting.
* It uses the tracker's own selector and naming rules, so "passes the test" and "recorded by name" mean the same thing.
* By default it requires an **explicit** `data-journey` (or a named ancestor, or `data-journey-ignore`). Passing `{ autoClickNames: true }` also accepts elements the tracker can name from attributes. Requiring explicit names is the stricter, recommended setting, because explicit names survive test ID and label changes and keep workbook history continuous.

This replaces the test TMS planned to write themselves ("fails when a `<button>` has neither `data-journey` nor `data-journey-ignore`"), and covers links, tabs and menu items too.

### 3.6 Application pattern: a required `journey` prop

The only way to make a missing name impossible is to make it a compile error. That belongs in the application, not the package, so it is documented rather than shipped:

```tsx
type ButtonProps = React.ButtonHTMLAttributes<HTMLButtonElement> & { journey: string }

export function Button({ journey, ...props }: ButtonProps) {
  return <button data-journey={journey} {...props} />
}
```

Teams with a shared component library get compile-time enforcement for the components that use it; the test helper covers everything else.

### 3.7 Recommended setup for a new React app

```tsx
<UserJourneyProvider options={{
  endpoint: '/api/user-journey/events',
  getAccessToken: () => auth.user?.access_token,
  autoClickNames: true,
  warnUnnamed: import.meta.env.DEV,
}}>
```

Plus `assertJourneyNames(container)` in each page's component test, and a required `journey` prop on shared buttons.

## 4. Privacy

| Data | 11.0.1 | With this proposal |
|---|---|---|
| Paths (no query string or fragment) | Sent | Sent, unchanged |
| `data-journey` names | Sent | Sent, unchanged |
| Values of the `autoClickNames` attributes | Not read | Sent, **only when the app opts in**, and only the attributes it lists |
| Element text, form values | Never read | Never read |
| Role or tag name (`button`, `link`) | Not sent | Sent inside `(unnamed ...)` when opted in; contains no data |

The one new exposure is attribute values, and it is opt-in and configurable. The README warns that a templated `aria-label` is recorded as written and recommends listing only attributes whose values never hold personal data. Teams that want zero risk use `autoClickNames: ['data-testid']`.

## 5. Compatibility

* **No API changes** on the server. Auto names and `(unnamed ...)` targets are ordinary click targets: non-empty and well under the 128-character limit the endpoint enforces (the tracker already truncates to 128).
* **Default behaviour is unchanged.** All new options are off by default; an explicit `data-journey` always wins; the existing tests pass unchanged.
* **Telemetry volume** rises for apps that turn on `autoClickNames`, by the number of clicks on previously untracked interactive elements. This is why it is opt-in in 11.x.
* **Workbook.** The new report is an extra item; existing reports are unchanged. Saved copies of the workbook keep working; they only lack the new report until replaced.
* **New package entry point** `./testing` in `exports`. Additive.

## 6. Alternatives considered

| Alternative | Why not (now) |
|---|---|
| Name clicks from element text | Text can contain data ("Export 12 orders", a customer name), and the package promises never to read it |
| Turn `autoClickNames` on by default | Changes telemetry volume and cost in a minor release. Reconsider for 12.0 |
| Drop clicks without a usable attribute instead of recording `(unnamed ...)` | Keeps the failure silent, which is the problem being solved |
| ESLint plugin flagging `<button>` without `data-journey` | Catches only literal JSX elements, not component libraries or rendered output; a separate package to publish and maintain. The test helper checks the rendered DOM instead. Revisit if teams ask |
| Detect React `onClick` on any element | React attaches handlers at the root, so the DOM does not show them; reading React internals is fragile |
| Send an `auto: true` flag so the workbook can tell auto names from explicit ones | Needs a server schema change. `(unnamed ...)` already marks the cases that need attention; auto names from `data-testid` are usable as they are |
| Use CSS classes or DOM position as names | Unstable across builds and redesigns, and meaningless in a report |

## 7. Implementation

| File | Change |
|---|---|
| `src/CoreDesign.UserJourney.React/src/clickables.ts` | New. Clickable selector, naming rules, React ID filter, shared by tracker and test helper |
| `src/CoreDesign.UserJourney.React/src/index.ts` | Options `autoClickNames`, `ignoreAttribute`, `warnUnnamed`; click handler uses the shared rules |
| `src/CoreDesign.UserJourney.React/src/testing.ts` | New entry point: `assertJourneyNames`, `findUnnamedClickables` |
| `src/CoreDesign.UserJourney.React/package.json` | `./testing` export |
| `src/CoreDesign.UserJourney.React/test/clicks.test.ts` | New. 16 tests: naming order, ignore, React IDs, unnamed targets, warnings, back compatibility |
| `src/CoreDesign.UserJourney.React/test/testing.test.ts` | New. 7 tests for the helper |
| `src/CoreDesign.UserJourney/workbooks/user-journey.workbook.json` | New report: Clicks without a name |
| `src/CoreDesign.UserJourney.React/README.md` | New section "Not missing clicks"; options table; privacy |
| `skills/coredesign/references/user-journey.md`, `CoreDesignUserGuide.md`, `RELEASE_NOTES.md` | Same guidance |

Results: 37 tests pass (14 existing, 23 new); `tsc` typecheck and build are clean; the workbook's queries pass the Kusto parser and a semantic check against an Application Insights schema. Runtime code is about 4.4 kB gzipped (tracker, naming rules and React bindings).

The npm version is still `11.0.1` in `package.json`. All CoreDesign packages share one version, so the bump is left for release.

## 8. How to test

### 8.1 Package tests

```powershell
cd src\CoreDesign.UserJourney.React
npm test
npm run typecheck
npm run build
```

jsdom prints "Not implemented: navigation to another Document" when a test clicks an `<a href>`. It is harmless; the tests pass.

### 8.2 In Sample.React

`Sample.React` depends on the published `^11.0.1` package, which does not have the new options yet. Install the local build without changing `package.json`:

```powershell
cd src\CoreDesign.UserJourney.React
npm pack
cd ..\..\Sample\src\Sample.React
npm install --no-save ..\..\..\src\CoreDesign.UserJourney.React\coredesign.dev-user-journey-11.0.1.tgz
```

Then in `src/journey.tsx` add `autoClickNames: true, warnUnnamed: true` to the options, and add a test button without a name to a page, for example `<button>Try me</button>` and `<button aria-label="Refresh forecasts">R</button>`. Run the app through the AppHost and check:

| Check | Expected |
|---|---|
| Click a `data-journey` nav button | Recorded with its name, as before; no warning |
| Click `<button>Try me</button>` | Console warning once; recorded as `(unnamed button)` |
| Click it again | No second warning; recorded again |
| Click the `aria-label` button | Recorded as `Refresh forecasts` |
| Wrap one in `<div data-journey-ignore>` | Not recorded, no warning |
| Remove `autoClickNames` | Unnamed clicks not recorded; warning says "not recorded" |
| Network tab | `POST /api/user-journey/events` returns 202 |

Run `npm install` afterwards (or delete `node_modules`) to return to the published package. Do not commit the `.tgz` (`*.tgz` is ignored).

### 8.3 In Application Insights

With the Sample deployed (or TMS DEV on the local build), import the updated workbook and confirm:

* The timeline shows `Interacted: (unnamed button) on /weather`.
* **Clicks without a name** lists it by page, with counts and users.
* With no unnamed clicks, the report shows its "No unnamed clicks" message.

## 9. Open questions

1. **Version.** This adds public options and an entry point, so it is a minor release (11.1.0) by the CoreDesign versioning rules. Should it ship with the workbook fixes in the same release?
2. **Default in 12.0.** Should `autoClickNames` default to on in the next major version, once teams have run it?
3. **`aria-label` in the default list.** It identifies icon buttons well but is the attribute most likely to be templated with data. Keep it (current choice), or default to `data-testid`, `name`, `id` only?
4. **ESLint rule.** Worth a separate package if teams want feedback in the editor rather than in tests?
5. **Sample.React.** Once the package is published, turn on `autoClickNames` and `warnUnnamed` in the Sample and add an `assertJourneyNames` test, so the Sample shows the recommended setup.
