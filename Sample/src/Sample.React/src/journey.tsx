import type { ReactNode } from 'react'
import { useAuth } from 'react-oidc-context'
import { UserJourneyProvider } from '@coredesign.dev/user-journey/react'

// Sample.Api maps the endpoint with app.MapUserJourneyEvents(); the Vite proxy forwards /api to it.
const JourneyEndpoint = '/api/user-journey/events'

/** Sends this signed-in user's page views and data-journey clicks to the API. */
export default function JourneyProvider({ children }: { children: ReactNode }) {
  const auth = useAuth()

  return (
    <UserJourneyProvider options={{ endpoint: JourneyEndpoint, getAccessToken: () => auth.user?.access_token }}>
      {children}
    </UserJourneyProvider>
  )
}
