import { useState } from 'react'
import { useAuth } from 'react-oidc-context'
import { useJourneyPage } from '@coredesign.dev/user-journey/react'
import Layout, { type Page } from './components/Layout'
import Home from './pages/Home'
import WeatherForecasts from './components/WeatherForecasts'

export default function App() {
  const auth = useAuth()
  const [page, setPage] = useState<Page>('home')
  // This app switches pages with state rather than a router, so the page name stands in for the path.
  useJourneyPage(`/${page}`)

  if (auth.isLoading) {
    return (
      <div className="container mt-5">
        <p className="text-muted">Loading...</p>
      </div>
    )
  }

  if (auth.error) {
    return (
      <div className="container mt-5">
        <div className="alert alert-danger">Authentication error: {auth.error.message}</div>
      </div>
    )
  }

  if (!auth.isAuthenticated) {
    return (
      <div className="container mt-5 text-center">
        <h1 className="mb-4">Sample React</h1>
        <button className="btn btn-primary" onClick={() => void auth.signinRedirect()}>
          Sign in
        </button>
      </div>
    )
  }

  return (
    <Layout page={page} onNavigate={setPage}>
      {page === 'home'
        ? <Home />
        : <WeatherForecasts accessToken={auth.user?.access_token ?? ''} />
      }
    </Layout>
  )
}
