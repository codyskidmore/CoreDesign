import type { AuthProviderProps } from 'react-oidc-context'

export const oidcConfig: AuthProviderProps = {
  authority: import.meta.env.VITE_AUTHORITY,
  client_id: 'sample-react',
  redirect_uri: `${window.location.origin}/`,
  post_logout_redirect_uri: `${window.location.origin}/`,
  scope: 'openid profile email https://api.sampleapi.local',
  onSigninCallback: () => {
    window.history.replaceState({}, document.title, '/')
  },
}
