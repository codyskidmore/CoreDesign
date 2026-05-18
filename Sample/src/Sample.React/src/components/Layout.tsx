import type { ReactNode } from 'react'
import { useAuth } from 'react-oidc-context'

export type Page = 'home' | 'weather'

interface Props {
  page: Page
  onNavigate: (page: Page) => void
  children: ReactNode
}

export default function Layout({ page, onNavigate, children }: Props) {
  const auth = useAuth()
  const profile = auth.user?.profile
  const userName = profile?.name ?? profile?.preferred_username ?? 'User'

  return (
    <div className="d-flex" style={{ minHeight: '100vh' }}>
      <nav
        className="d-flex flex-column px-2 py-3"
        style={{ width: '220px', background: '#1b2a4a', flexShrink: 0 }}
      >
        <span className="fw-semibold fs-6 mb-3 px-2 text-white">Sample React</span>
        <ul className="nav flex-column w-100">
          <li className="nav-item">
            <button
              className={`nav-link btn btn-link text-start w-100 px-2 ${page === 'home' ? 'text-white fw-semibold' : 'text-white-50'}`}
              onClick={() => onNavigate('home')}
            >
              Home
            </button>
          </li>
          <li className="nav-item">
            <button
              className={`nav-link btn btn-link text-start w-100 px-2 ${page === 'weather' ? 'text-white fw-semibold' : 'text-white-50'}`}
              onClick={() => onNavigate('weather')}
            >
              Weather Forecasts
            </button>
          </li>
        </ul>
        <div className="mt-auto pt-3 border-top border-secondary px-2">
          <small className="d-block text-white-50">Auth provider in use</small>
          <small className="text-white">Local Identity Server</small>
        </div>
      </nav>
      <div className="d-flex flex-column flex-grow-1">
        <div className="d-flex justify-content-end align-items-center px-4 py-2 border-bottom bg-white">
          <span className="me-3 small">Hello, {userName}!</span>
          <button
            className="btn btn-sm btn-outline-secondary"
            onClick={() => void auth.signoutRedirect()}
          >
            Sign out
          </button>
        </div>
        <article className="p-4">
          {children}
        </article>
      </div>
    </div>
  )
}
