import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { AuthProvider } from 'react-oidc-context'
import JourneyProvider from './journey'
import { oidcConfig } from './auth'
import App from './App'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <AuthProvider {...oidcConfig}>
      <JourneyProvider>
        <App />
      </JourneyProvider>
    </AuthProvider>
  </StrictMode>,
)
