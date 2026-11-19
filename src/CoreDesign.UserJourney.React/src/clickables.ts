/**
 * Which elements count as clickable, and how a click on one is named. Shared by the tracker and by the
 * `@coredesign.dev/user-journey/testing` helpers, so tests check exactly what the tracker records.
 *
 * Element text is never read: it can contain data, and the package promises not to record it.
 */

/** Attribute that names a clickable element. */
export const DefaultClickAttribute = 'data-journey'

/** Attribute that stops an element (and everything inside it) from being auto-named or reported as unnamed. */
export const DefaultIgnoreAttribute = 'data-journey-ignore'

/** Attributes tried, in order, to name a clickable element that has no `data-journey`. */
export const DefaultAutoClickNames: readonly string[] = ['data-testid', 'aria-label', 'name', 'id']

/**
 * Elements a user clicks to do something. A `<div onClick>` cannot be detected from the DOM, because React
 * attaches its handlers at the root; give such elements a role (accessibility lint rules ask for one anyway).
 */
export const ClickableSelector = [
  'button',
  'a[href]',
  'summary',
  'input[type="button"]',
  'input[type="submit"]',
  'input[type="reset"]',
  'input[type="checkbox"]',
  'input[type="radio"]',
  '[role="button"]',
  '[role="link"]',
  '[role="tab"]',
  '[role="menuitem"]',
  '[role="menuitemcheckbox"]',
  '[role="menuitemradio"]',
  '[role="option"]',
  '[role="switch"]',
  '[role="checkbox"]',
  '[role="radio"]',
].join(', ')

/** Prefix of the target recorded for a clickable element with no name. */
export const UnnamedPrefix = '(unnamed '

/** Settings that decide how clicks are named. */
export interface ClickNaming {
  clickAttribute: string
  ignoreAttribute: string
  /** Attributes tried when `clickAttribute` is missing, or `undefined` to record only named elements. */
  autoClickNames: readonly string[] | undefined
}

/** How a click resolved. */
export interface ClickResolution {
  /** The name to record, or `undefined` when nothing is recorded. */
  target: string | undefined
  /** True when the name came from `clickAttribute`. */
  explicit: boolean
  /** The clickable element, when the click was on one without an explicit name. */
  clickable?: Element
  /** True when the clickable element is inside an element marked with `ignoreAttribute`. */
  ignored: boolean
}

/**
 * Turns the `autoClickNames` option into a list of attributes: `true` means the defaults, `false` or
 * `undefined` means off.
 */
export function normalizeAutoClickNames(value: boolean | readonly string[] | undefined): readonly string[] | undefined {
  if (value === true) return DefaultAutoClickNames
  if (!value || value.length === 0) return undefined
  return value
}

/** True for IDs React generates with `useId`: `:r1:` (18), `«r1»` (19.0 and 19.1), `_r_1_` (19.2 and later). */
export function isGeneratedId(value: string): boolean {
  return /^(:.*:|«.*»|_[rR]_.*_)$/.test(value)
}

/** The first stable value among `attributes` on the element, or `undefined`. */
export function autoClickName(element: Element, attributes: readonly string[]): string | undefined {
  for (const attribute of attributes) {
    const value = element.getAttribute(attribute)?.trim()
    if (value && !isGeneratedId(value)) return value
  }
  return undefined
}

/** A short, data-free description of an element: its role, or its tag. */
export function describeClickable(element: Element): string {
  const role = element.getAttribute('role')?.trim()
  if (role) return role
  const tag = element.tagName.toLowerCase()
  if (tag === 'a') return 'link'
  if (tag === 'input') return element.getAttribute('type')?.toLowerCase() || 'input'
  return tag
}

/** The target recorded for a clickable element with no name, such as `(unnamed button)`. */
export function unnamedTarget(element: Element): string {
  return `${UnnamedPrefix}${describeClickable(element)})`
}

/** The explicit name from `clickAttribute` on the element or an ancestor, or `undefined`. */
export function explicitName(element: Element, clickAttribute: string): string | undefined {
  return element.closest(`[${clickAttribute}]`)?.getAttribute(clickAttribute)?.trim() || undefined
}

/**
 * Decides what a click on `origin` records. An explicit `clickAttribute` on the element or an ancestor
 * always wins. Otherwise, when auto-naming is on, the nearest clickable element is named from
 * `autoClickNames`, or recorded as `(unnamed <role or tag>)` so the gap shows up in the workbook.
 */
export function resolveClick(origin: Element, naming: ClickNaming): ClickResolution {
  const named = explicitName(origin, naming.clickAttribute)
  if (named) return { target: named, explicit: true, ignored: false }

  const clickable = origin.closest(ClickableSelector) ?? undefined
  if (!clickable) return { target: undefined, explicit: false, ignored: false }
  if (clickable.closest(`[${naming.ignoreAttribute}]`)) {
    return { target: undefined, explicit: false, clickable, ignored: true }
  }
  if (!naming.autoClickNames) return { target: undefined, explicit: false, clickable, ignored: false }

  const target = autoClickName(clickable, naming.autoClickNames) ?? unnamedTarget(clickable)
  return { target, explicit: false, clickable, ignored: false }
}

/** The element's opening tag, shortened, for messages. */
export function openingTag(element: Element, maxLength = 120): string {
  const html = element.outerHTML
  const end = html.indexOf('>')
  const tag = end >= 0 ? html.slice(0, end + 1) : html
  return tag.length > maxLength ? `${tag.slice(0, maxLength - 4)} ...>` : tag
}
