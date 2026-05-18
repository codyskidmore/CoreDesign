import { useAuth } from 'react-oidc-context'

export default function Home() {
  const { user } = useAuth()
  const profile = user?.profile
  const userName = profile?.name ?? profile?.preferred_username ?? 'User'

  const claims = Object.entries((profile ?? {}) as Record<string, unknown>)
    .filter(([, v]) => v !== null && v !== undefined && typeof v !== 'object')
    .map(([type, value]) => ({ type, value: String(value) }))
    .sort((a, b) => a.type.localeCompare(b.type))

  return (
    <>
      <h1>Welcome, {userName}!</h1>
      <p className="text-muted">
        Authenticated via <strong>Local Identity Server</strong>
      </p>
      <h2 className="mt-4">Your Claims</h2>
      <table className="table table-sm table-bordered">
        <thead className="table-dark">
          <tr>
            <th>Claim Type</th>
            <th>Value</th>
          </tr>
        </thead>
        <tbody>
          {claims.map(({ type, value }) => (
            <tr key={type}>
              <td>{type}</td>
              <td>{value}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  )
}
