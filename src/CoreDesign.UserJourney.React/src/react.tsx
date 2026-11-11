import { createContext, useContext, useEffect, useMemo, useRef, type ReactNode } from 'react'
import { createUserJourney, type UserJourneyOptions, type UserJourneyTracker, type ViewedItem } from './index.js'

export type { UserJourneyOptions, UserJourneyTracker, ViewedItem } from './index.js'

const JourneyContext = createContext<UserJourneyTracker | null>(null)

export interface UserJourneyProviderProps {
  options: UserJourneyOptions
  children?: ReactNode
}

/**
 * Starts tracking for everything inside it. Put it inside your auth provider so `getAccessToken` can
 * read the current token. Options are read once, except `getAccessToken`, which always uses the
 * latest function you pass.
 */
export function UserJourneyProvider({ options, children }: UserJourneyProviderProps) {
  const latest = useRef(options)
  latest.current = options

  const tracker = useMemo(
    () => createUserJourney({ ...options, getAccessToken: () => latest.current.getAccessToken() }),
    // Created once; getAccessToken reads the latest options through the ref.
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [],
  )

  useEffect(() => {
    tracker.start()
    return () => tracker.stop()
  }, [tracker])

  return <JourneyContext.Provider value={tracker}>{children}</JourneyContext.Provider>
}

/** The tracker from the nearest `UserJourneyProvider`, for manual `page` and `click` calls. */
export function useUserJourney(): UserJourneyTracker {
  const tracker = useContext(JourneyContext)
  if (!tracker) throw new Error('useUserJourney must be used inside a UserJourneyProvider.')
  return tracker
}

/**
 * Records a page view when `path` changes. Pass your router's current path, for example
 * `useJourneyPage(useLocation().pathname)` with React Router, or any string that names the page.
 */
export function useJourneyPage(path: string, item?: ViewedItem): void {
  const tracker = useUserJourney()
  const itemType = item?.type
  const itemName = item?.name

  useEffect(() => {
    tracker.page(path, itemType ? { type: itemType, name: itemName } : undefined)
  }, [tracker, path, itemType, itemName])
}
