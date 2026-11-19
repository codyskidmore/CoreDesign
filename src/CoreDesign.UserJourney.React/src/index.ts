/**
 * Browser side of CoreDesign.UserJourney. Records page views and clicks for the signed-in user and
 * posts them in small batches to the endpoint mapped by `app.MapUserJourneyEvents()` in the API.
 *
 * Privacy: it records only paths (never query strings or fragments), the names you put in
 * `data-journey` attributes and, with `autoClickNames`, the attribute values you choose. It never reads
 * form values or element text.
 */

import {
  DefaultClickAttribute,
  DefaultIgnoreAttribute,
  normalizeAutoClickNames,
  openingTag,
  resolveClick,
  type ClickNaming,
} from './clickables.js'

export { DefaultAutoClickNames, DefaultClickAttribute, DefaultIgnoreAttribute } from './clickables.js'

export type JourneyEventKind = 'page' | 'click'

/** One event, in the shape the API endpoint accepts. */
export interface JourneyEvent {
  kind: JourneyEventKind
  path: string
  target?: string
  itemType?: string
  item?: string
}

/** What a page showed, for the "Items viewed" report. Never personal data. */
export interface ViewedItem {
  type: string
  name?: string | null
}

export interface UserJourneyOptions {
  /** URL of the API endpoint, for example `/api/user-journey/events`. */
  endpoint: string
  /**
   * Returns the signed-in user's access token. Events are only sent while it returns a token, because a
   * journey belongs to a signed-in user.
   */
  getAccessToken: () => string | null | undefined | Promise<string | null | undefined>
  /** How often queued events are sent, in milliseconds. Default 5000. */
  flushIntervalMs?: number
  /** Events per request; reaching it sends immediately. Default 20 (the API accepts up to 50). */
  maxBatchSize?: number
  /** Record clicks on elements marked with `clickAttribute` (and, with `autoClickNames`, on the rest). Default true. */
  captureClicks?: boolean
  /**
   * Also record clicks on clickable elements (buttons, links, `role="button"` and similar) that have no
   * `clickAttribute`. `true` names them from `data-testid`, `aria-label`, `name` or `id`, in that order;
   * pass your own list of attributes to change that. An element with none of them is recorded as
   * `(unnamed button)`, `(unnamed link)` and so on, so missed names show up in the workbook instead of
   * disappearing. Element text is never used. Default off.
   */
  autoClickNames?: boolean | readonly string[]
  /** Attribute that excludes an element and its children from auto-naming and warnings. Default `data-journey-ignore`. */
  ignoreAttribute?: string
  /**
   * Log a console warning the first time a clickable element without `clickAttribute` is clicked, so
   * developers see missed names while they work. Turn it on in development only, for example
   * `warnUnnamed: import.meta.env.DEV`. Default false.
   */
  warnUnnamed?: boolean
  /** Attribute whose value names a clickable element. Default `data-journey`. */
  clickAttribute?: string
  /** Current page path for click events. Default `window.location.pathname`. */
  getPath?: () => string
  /** Fetch implementation, for tests. Default the global `fetch`. */
  fetch?: typeof fetch
}

export interface UserJourneyTracker {
  /** Records a page view. Call it when the page (route) changes. */
  page(path: string, item?: ViewedItem): void
  /** Records a click on a named element. Usually automatic through `data-journey` attributes. */
  click(target: string, path?: string): void
  /** Sends queued events now. */
  flush(options?: { keepalive?: boolean }): Promise<void>
  /** Starts click capture and the send timer. */
  start(): void
  /** Stops click capture and the timer, and sends what is queued. */
  stop(): void
}

const DefaultFlushIntervalMs = 5000
const DefaultMaxBatchSize = 20
const MaxPathLength = 512
const MaxNameLength = 128
const MaxItemLength = 256

/** Removes the query string and fragment, so search terms and tokens are never recorded. */
export function cleanPath(path: string): string {
  const end = path.search(/[?#]/)
  const clean = end >= 0 ? path.slice(0, end) : path
  const absolute = clean.startsWith('/') ? clean : `/${clean}`
  return absolute.slice(0, MaxPathLength)
}

export function createUserJourney(options: UserJourneyOptions): UserJourneyTracker {
  const flushIntervalMs = options.flushIntervalMs ?? DefaultFlushIntervalMs
  const maxBatchSize = options.maxBatchSize ?? DefaultMaxBatchSize
  const naming: ClickNaming = {
    clickAttribute: options.clickAttribute ?? DefaultClickAttribute,
    ignoreAttribute: options.ignoreAttribute ?? DefaultIgnoreAttribute,
    autoClickNames: normalizeAutoClickNames(options.autoClickNames),
  }
  const warned = new WeakSet<Element>()
  const getPath = options.getPath ?? (() => window.location.pathname)
  const send = options.fetch ?? ((input, init) => fetch(input, init))

  let queue: JourneyEvent[] = []
  let timer: ReturnType<typeof setInterval> | undefined
  let started = false

  const enqueue = (event: JourneyEvent) => {
    queue.push(event)
    if (queue.length >= maxBatchSize) void flush()
  }

  async function flush(flushOptions?: { keepalive?: boolean }): Promise<void> {
    if (queue.length === 0) return
    const batch = queue.slice(0, maxBatchSize)
    queue = queue.slice(batch.length)

    const token = await options.getAccessToken()
    if (!token) return // not signed in: drop, a journey belongs to a signed-in user

    try {
      await send(options.endpoint, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: JSON.stringify(batch),
        keepalive: flushOptions?.keepalive ?? false,
      })
    } catch {
      // Best effort: tracking must never break the app.
    }

    if (queue.length > 0) await flush(flushOptions)
  }

  /** Warns once per element, so a button clicked repeatedly does not flood the console. */
  const warnUnnamed = (element: Element, recordedAs: string | undefined) => {
    if (warned.has(element)) return
    warned.add(element)
    const recorded = recordedAs ? `It was recorded as "${recordedAs}".` : 'The click was not recorded.'
    console.warn(
      `[user-journey] ${openingTag(element)} on ${cleanPath(getPath())} has no ${naming.clickAttribute} attribute. ` +
        `${recorded} Add ${naming.clickAttribute}="name" to record it by name, or ${naming.ignoreAttribute} to skip it.`,
    )
  }

  const onClick = (event: MouseEvent) => {
    const origin = event.target as Element | null
    if (!origin?.closest) return
    const resolution = resolveClick(origin, naming)
    if (options.warnUnnamed && !resolution.explicit && !resolution.ignored && resolution.clickable) {
      warnUnnamed(resolution.clickable, resolution.target)
    }
    if (resolution.target) tracker.click(resolution.target)
  }

  const onHidden = () => {
    if (document.visibilityState === 'hidden') void flush({ keepalive: true })
  }

  const tracker: UserJourneyTracker = {
    page(path, item) {
      const event: JourneyEvent = { kind: 'page', path: cleanPath(path) }
      if (item?.name) {
        event.itemType = item.type.slice(0, MaxNameLength)
        event.item = String(item.name).slice(0, MaxItemLength)
      }
      enqueue(event)
    },

    click(target, path) {
      enqueue({ kind: 'click', path: cleanPath(path ?? getPath()), target: target.slice(0, MaxNameLength) })
    },

    flush,

    start() {
      if (started) return
      started = true
      if (options.captureClicks ?? true) document.addEventListener('click', onClick, true)
      document.addEventListener('visibilitychange', onHidden)
      timer = setInterval(() => void flush(), flushIntervalMs)
    },

    stop() {
      if (!started) return
      started = false
      document.removeEventListener('click', onClick, true)
      document.removeEventListener('visibilitychange', onHidden)
      if (timer !== undefined) clearInterval(timer)
      timer = undefined
      void flush({ keepalive: true })
    },
  }

  return tracker
}
