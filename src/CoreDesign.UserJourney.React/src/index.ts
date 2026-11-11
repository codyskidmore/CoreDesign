/**
 * Browser side of CoreDesign.UserJourney. Records page views and clicks for the signed-in user and
 * posts them in small batches to the endpoint mapped by `app.MapUserJourneyEvents()` in the API.
 *
 * Privacy: it records only paths (never query strings or fragments) and the names you put in
 * `data-journey` attributes. It never reads form values or element text.
 */

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
  /** Record clicks on elements marked with `clickAttribute`. Default true. */
  captureClicks?: boolean
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
const DefaultClickAttribute = 'data-journey'
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
  const clickAttribute = options.clickAttribute ?? DefaultClickAttribute
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

  const onClick = (event: MouseEvent) => {
    const element = (event.target as Element | null)?.closest?.(`[${clickAttribute}]`)
    const target = element?.getAttribute(clickAttribute)?.trim()
    if (target) tracker.click(target)
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
