import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { cleanPath, createUserJourney, type JourneyEvent, type UserJourneyOptions } from '../src/index'

const Endpoint = '/api/user-journey/events'

function setup(overrides: Partial<UserJourneyOptions> = {}) {
  const sent: { url: string; init: RequestInit; events: JourneyEvent[] }[] = []
  const fetchMock = vi.fn(async (url: RequestInfo | URL, init?: RequestInit) => {
    sent.push({ url: String(url), init: init!, events: JSON.parse(String(init!.body)) })
    return new Response(null, { status: 202 })
  })
  const tracker = createUserJourney({
    endpoint: Endpoint,
    getAccessToken: () => 'token-123',
    fetch: fetchMock as unknown as typeof fetch,
    getPath: () => '/current',
    ...overrides,
  })
  return { tracker, sent, fetchMock }
}

describe('cleanPath', () => {
  it('removes query strings and fragments', () => {
    expect(cleanPath('/search?q=secret#top')).toBe('/search')
  })

  it('makes paths absolute', () => {
    expect(cleanPath('weather')).toBe('/weather')
  })
})

describe('createUserJourney', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  it('sends queued events with the bearer token', async () => {
    const { tracker, sent } = setup()

    tracker.page('/products/42?ref=email', { type: 'Product', name: 'Sprocket' })
    tracker.click('add-to-cart')
    await tracker.flush()

    expect(sent).toHaveLength(1)
    expect(sent[0].url).toBe(Endpoint)
    expect((sent[0].init.headers as Record<string, string>).Authorization).toBe('Bearer token-123')
    expect(sent[0].events).toEqual([
      { kind: 'page', path: '/products/42', itemType: 'Product', item: 'Sprocket' },
      { kind: 'click', path: '/current', target: 'add-to-cart' },
    ])
  })

  it('sends nothing when the user is not signed in', async () => {
    const { tracker, fetchMock } = setup({ getAccessToken: () => undefined })

    tracker.page('/home')
    await tracker.flush()

    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('omits the item when it has no name', async () => {
    const { tracker, sent } = setup()

    tracker.page('/products', { type: 'Product', name: null })
    await tracker.flush()

    expect(sent[0].events[0]).toEqual({ kind: 'page', path: '/products' })
  })

  it('sends immediately when the batch is full', async () => {
    const { tracker, fetchMock } = setup({ maxBatchSize: 2 })

    tracker.page('/a')
    tracker.page('/b')
    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(1))
  })

  it('splits large queues into batches', async () => {
    const { tracker, sent } = setup({ maxBatchSize: 3 })
    tracker.start()
    tracker.stop() // stop so no automatic sends interfere

    for (const p of ['/1', '/2']) tracker.page(p)
    await tracker.flush()
    for (const p of ['/3', '/4', '/5', '/6', '/7']) tracker.page(p)
    await tracker.flush()

    expect(sent.flatMap(s => s.events.map(e => e.path))).toEqual(['/1', '/2', '/3', '/4', '/5', '/6', '/7'])
    expect(sent.every(s => s.events.length <= 3)).toBe(true)
  })

  it('captures clicks on data-journey elements, including their children', async () => {
    const { tracker, sent } = setup()
    document.body.innerHTML = '<button data-journey="save-forecast"><span id="inner">Save</span></button><button id="plain">Plain</button>'
    tracker.start()

    document.getElementById('inner')!.click()
    document.getElementById('plain')!.click()
    await tracker.flush()
    tracker.stop()

    expect(sent[0].events).toEqual([{ kind: 'click', path: '/current', target: 'save-forecast' }])
  })

  it('does not capture clicks when disabled', async () => {
    const { tracker, fetchMock } = setup({ captureClicks: false })
    document.body.innerHTML = '<button data-journey="x">X</button>'
    tracker.start()

    document.querySelector('button')!.click()
    await tracker.flush()
    tracker.stop()

    expect(fetchMock).not.toHaveBeenCalled()
  })

  it('sends on the timer', async () => {
    vi.useFakeTimers()
    const { tracker, fetchMock } = setup({ flushIntervalMs: 1000 })
    tracker.start()

    tracker.page('/home')
    await vi.advanceTimersByTimeAsync(1000)
    tracker.stop()

    expect(fetchMock).toHaveBeenCalledTimes(1)
  })

  it('never throws when the network fails', async () => {
    const { tracker } = setup({ fetch: (async () => { throw new Error('offline') }) as unknown as typeof fetch })

    tracker.page('/home')
    await expect(tracker.flush()).resolves.toBeUndefined()
  })
})
