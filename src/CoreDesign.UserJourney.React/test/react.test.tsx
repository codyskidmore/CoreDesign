import { act, useState } from 'react'
import { createRoot } from 'react-dom/client'
import { describe, expect, it, vi } from 'vitest'
import { UserJourneyProvider, useJourneyPage, useUserJourney } from '../src/react'
import type { JourneyEvent } from '../src/index'

// Tell React this is a test environment so act() works without warnings.
;(globalThis as { IS_REACT_ACT_ENVIRONMENT?: boolean }).IS_REACT_ACT_ENVIRONMENT = true

function render(element: React.ReactNode) {
  const container = document.createElement('div')
  document.body.appendChild(container)
  const root = createRoot(container)
  act(() => root.render(element))
  return { container, root }
}

describe('React bindings', () => {
  it('records a page view on mount and when the path changes', async () => {
    const sent: JourneyEvent[] = []
    const fetchMock = vi.fn(async (_url: RequestInfo | URL, init?: RequestInit) => {
      sent.push(...JSON.parse(String(init!.body)))
      return new Response(null, { status: 202 })
    })
    let tracker: ReturnType<typeof useUserJourney> | undefined
    let navigate: (path: string) => void = () => {}

    function Page() {
      const [path, setPath] = useState('/home')
      navigate = setPath
      useJourneyPage(path)
      tracker = useUserJourney()
      return null
    }

    const { root } = render(
      <UserJourneyProvider options={{ endpoint: '/events', getAccessToken: () => 't', fetch: fetchMock as unknown as typeof fetch }}>
        <Page />
      </UserJourneyProvider>,
    )
    act(() => navigate('/weather'))
    await act(async () => { await tracker!.flush() })
    act(() => root.unmount())

    expect(sent.map(e => e.path)).toEqual(['/home', '/weather'])
  })

  it('uses the latest access token', async () => {
    const tokens: string[] = []
    const fetchMock = vi.fn(async (_url: RequestInfo | URL, init?: RequestInit) => {
      tokens.push((init!.headers as Record<string, string>).Authorization)
      return new Response(null, { status: 202 })
    })
    let tracker: ReturnType<typeof useUserJourney> | undefined
    let setToken: (token: string) => void = () => {}

    function Probe() {
      tracker = useUserJourney()
      return null
    }

    function App() {
      const [token, update] = useState('first')
      setToken = update
      return (
        <UserJourneyProvider options={{ endpoint: '/events', getAccessToken: () => token, fetch: fetchMock as unknown as typeof fetch }}>
          <Probe />
        </UserJourneyProvider>
      )
    }

    const { root } = render(<App />)
    act(() => setToken('second'))
    tracker!.page('/home')
    await act(async () => { await tracker!.flush() })
    act(() => root.unmount())

    expect(tokens).toEqual(['Bearer second'])
  })

  it('throws a clear error outside the provider', () => {
    function Orphan() {
      useUserJourney()
      return null
    }
    const error = vi.spyOn(console, 'error').mockImplementation(() => {})

    expect(() => render(<Orphan />)).toThrow('inside a UserJourneyProvider')
    error.mockRestore()
  })
})
