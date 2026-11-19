/**
 * Test helpers for `@coredesign.dev/user-journey`: fail a test when a clickable element has no journey
 * name, so a new or redesigned screen cannot silently drop click tracking.
 *
 * Works with any test runner and DOM (Vitest, Jest, jsdom, happy-dom, a real browser); it throws a plain
 * `Error`, so no `expect` is needed.
 *
 * ```ts
 * import { assertJourneyNames } from '@coredesign.dev/user-journey/testing'
 *
 * it('names every clickable element', () => {
 *   const { container } = render(<OrdersPage />)
 *   assertJourneyNames(container)
 * })
 * ```
 */

import {
  ClickableSelector,
  DefaultClickAttribute,
  DefaultIgnoreAttribute,
  autoClickName,
  explicitName,
  normalizeAutoClickNames,
  openingTag,
} from './clickables.js'

export { ClickableSelector } from './clickables.js'

export interface JourneyNameCheckOptions {
  /** Attribute that names clickable elements. Default `data-journey`; match the tracker's `clickAttribute`. */
  clickAttribute?: string
  /** Attribute that excludes an element and its children. Default `data-journey-ignore`. */
  ignoreAttribute?: string
  /**
   * Accept elements the tracker can name from these attributes (`true` for the tracker's defaults). Leave
   * it off to require an explicit `data-journey` on every clickable element, which keeps names stable
   * when test IDs or labels change. Default off.
   */
  autoClickNames?: boolean | readonly string[]
}

/** Clickable elements in `root` (including `root` itself) that would be recorded without a name. */
export function findUnnamedClickables(root: ParentNode = document, options: JourneyNameCheckOptions = {}): Element[] {
  const clickAttribute = options.clickAttribute ?? DefaultClickAttribute
  const ignoreAttribute = options.ignoreAttribute ?? DefaultIgnoreAttribute
  const autoNames = normalizeAutoClickNames(options.autoClickNames)

  const candidates = Array.from(root.querySelectorAll(ClickableSelector))
  if (root instanceof Element && root.matches(ClickableSelector)) candidates.unshift(root)

  return candidates.filter(
    (element) =>
      !explicitName(element, clickAttribute) &&
      !element.closest(`[${ignoreAttribute}]`) &&
      !(autoNames && autoClickName(element, autoNames)),
  )
}

/**
 * Throws when any clickable element in `root` has no journey name. The message lists each element's
 * opening tag, so the failing element is easy to find.
 */
export function assertJourneyNames(root: ParentNode = document, options: JourneyNameCheckOptions = {}): void {
  const unnamed = findUnnamedClickables(root, options)
  if (unnamed.length === 0) return

  const clickAttribute = options.clickAttribute ?? DefaultClickAttribute
  const ignoreAttribute = options.ignoreAttribute ?? DefaultIgnoreAttribute
  const shown = 20
  const lines = unnamed.slice(0, shown).map((element) => `  ${openingTag(element)}`)
  if (unnamed.length > shown) lines.push(`  ...and ${unnamed.length - shown} more`)
  throw new Error(
    `${unnamed.length} clickable element${unnamed.length === 1 ? ' has' : 's have'} no ${clickAttribute} name. ` +
      `Add ${clickAttribute}="name", or ${ignoreAttribute} to skip an element on purpose:\n${lines.join('\n')}`,
  )
}
