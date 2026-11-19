import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { createUserJourney, type JourneyEvent, type UserJourneyOptions } from '../src/index'
import { isGeneratedId } from '../src/clickables'

function setup(overrides: Partial<UserJourneyOptions> = {}) {
  const sent: JourneyEvent[] = []
  const fetchMock = vi.fn(async (_url: RequestInfo | URL, init?: RequestInit) => {
    sent.push(...JSON.parse(String(init!.body)))
    return new Response(null, { status: 202 })
  })
  const tracker = createUserJourney({
    endpoint: '/api/user-journey/events',
    getAccessToken: () => 'token',
    fetch: fetchMock as unknown as typeof fetch,
    getPath: () => '/orders',
    ...overrides,
  })
  tracker.start()
  return { tracker, sent }
}

/** Clicks the element matched by `selector` and returns the click targets that were sent. */
async function clickTargets(html: string, selector: string, overrides: Partial<UserJourneyOptions> = {}) {
  document.body.innerHTML = html
  const { tracker, sent } = setup(overrides)
  document.querySelector<HTMLElement>(selector)!.click()
  await tracker.flush()
  tracker.stop()
  return sent.filter((e) => e.kind === 'click').map((e) => e.target)
}

describe('click naming', () => {
  beforeEach(() => {
    document.body.innerHTML = ''
  })

  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('ignores unnamed clicks by default, as before', async () => {
    expect(await clickTargets('<button data-testid="save">Save</button>', 'button')).toEqual([])
  })

  it('prefers data-journey over auto names', async () => {
    const targets = await clickTargets('<button data-journey="save-order" data-testid="save">Save</button>', 'button', {
      autoClickNames: true,
    })
    expect(targets).toEqual(['save-order'])
  })

  it('keeps naming clicks inside a named container after the container name', async () => {
    const targets = await clickTargets('<nav data-journey="main-nav"><a href="/x" id="x-link">X</a></nav>', 'a', {
      autoClickNames: true,
    })
    expect(targets).toEqual(['main-nav'])
  })

  it('names unnamed elements from the default attributes, in order', async () => {
    const options = { autoClickNames: true }
    expect(await clickTargets('<button data-testid="save" aria-label="Save" id="b">Save</button>', 'button', options)).toEqual(['save'])
    expect(await clickTargets('<button aria-label="Close dialog" id="b">x</button>', 'button', options)).toEqual(['Close dialog'])
    expect(await clickTargets('<input type="submit" name="confirm" />', 'input', options)).toEqual(['confirm'])
    expect(await clickTargets('<a href="/help" id="help-link">Help</a>', 'a', options)).toEqual(['help-link'])
  })

  it('uses a custom attribute list', async () => {
    const targets = await clickTargets('<button data-testid="save" data-qa="save-qa">Save</button>', 'button', {
      autoClickNames: ['data-qa'],
    })
    expect(targets).toEqual(['save-qa'])
  })

  it('records an element with no usable attribute as unnamed, never by its text', async () => {
    const options = { autoClickNames: true }
    expect(await clickTargets('<button>Export 12 orders</button>', 'button', options)).toEqual(['(unnamed button)'])
    expect(await clickTargets('<a href="/orders/42">Order 42</a>', 'a', options)).toEqual(['(unnamed link)'])
    expect(await clickTargets('<div role="tab">Details</div>', 'div', options)).toEqual(['(unnamed tab)'])
    expect(await clickTargets('<input type="checkbox" />', 'input', options)).toEqual(['(unnamed checkbox)'])
  })

  it('skips React generated ids', async () => {
    const targets = await clickTargets('<button id="_r_1_">Save</button>', 'button', { autoClickNames: true })
    expect(targets).toEqual(['(unnamed button)'])
  })

  it('names clicks on children after the clickable element', async () => {
    const targets = await clickTargets('<button data-testid="save"><span class="icon">S</span></button>', 'span', {
      autoClickNames: true,
    })
    expect(targets).toEqual(['save'])
  })

  it('does not record clicks outside clickable elements', async () => {
    expect(await clickTargets('<p id="text">Just text</p>', 'p', { autoClickNames: true })).toEqual([])
  })

  it('skips elements inside data-journey-ignore', async () => {
    const html = '<div data-journey-ignore><button data-testid="carousel-next">Next</button></div>'
    expect(await clickTargets(html, 'button', { autoClickNames: true })).toEqual([])
  })

  it('records explicit names even inside data-journey-ignore', async () => {
    const html = '<div data-journey-ignore><button data-journey="checkout">Buy</button></div>'
    expect(await clickTargets(html, 'button', { autoClickNames: true })).toEqual(['checkout'])
  })

  it('falls back to auto names when data-journey is empty', async () => {
    const targets = await clickTargets('<button data-journey=" " data-testid="save">Save</button>', 'button', {
      autoClickNames: true,
    })
    expect(targets).toEqual(['save'])
  })
})

describe('warnUnnamed', () => {
  afterEach(() => {
    vi.restoreAllMocks()
  })

  it('warns once per unnamed element, without recording it when auto-naming is off', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    document.body.innerHTML = '<button class="primary">Save</button>'
    const { tracker, sent } = setup({ warnUnnamed: true })
    const button = document.querySelector('button')!

    button.click()
    button.click()
    await tracker.flush()
    tracker.stop()

    expect(sent).toEqual([])
    expect(warn).toHaveBeenCalledTimes(1)
    expect(warn.mock.calls[0][0]).toContain('<button class="primary"> on /orders has no data-journey attribute')
    expect(warn.mock.calls[0][0]).toContain('not recorded')
  })

  it('says what the click was recorded as when auto-naming is on', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    document.body.innerHTML = '<button data-testid="save">Save</button>'
    const { tracker } = setup({ warnUnnamed: true, autoClickNames: true })

    document.querySelector('button')!.click()
    tracker.stop()

    expect(warn.mock.calls[0][0]).toContain('recorded as "save"')
  })

  it('does not warn for named or ignored elements', async () => {
    const warn = vi.spyOn(console, 'warn').mockImplementation(() => {})
    document.body.innerHTML = '<button data-journey="save">Save</button><button data-journey-ignore>Close</button>'
    const { tracker } = setup({ warnUnnamed: true })

    document.querySelectorAll('button').forEach((b) => b.click())
    tracker.stop()

    expect(warn).not.toHaveBeenCalled()
  })
})

describe('isGeneratedId', () => {
  it('recognises every React useId format', () => {
    for (const id of [':r1:', '«r1»', '_r_1_', '_R_1a_']) expect(isGeneratedId(id)).toBe(true)
    for (const id of ['save', 'order_r_1', 'r1', 'help-link']) expect(isGeneratedId(id)).toBe(false)
  })
})
