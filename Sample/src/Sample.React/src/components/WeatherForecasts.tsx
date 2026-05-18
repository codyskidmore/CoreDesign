import { useEffect, useState } from 'react'
import { getWeatherForecasts, type WeatherForecast } from '../api'

interface Props {
  accessToken: string
}

export default function WeatherForecasts({ accessToken }: Props) {
  const [forecasts, setForecasts] = useState<WeatherForecast[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    getWeatherForecasts(accessToken)
      .then(setForecasts)
      .catch((err: unknown) =>
        setError(err instanceof Error ? err.message : 'Unknown error'),
      )
      .finally(() => setLoading(false))
  }, [accessToken])

  if (loading) return <p className="text-muted"><em>Loading forecasts...</em></p>

  if (error) {
    return (
      <div className="alert alert-danger">
        <strong>Error:</strong> {error}
      </div>
    )
  }

  return (
    <>
      <h1>Weather Forecasts</h1>
      <p className="text-muted">
        Data fetched from <strong>Sample.Api</strong> using your OIDC access token.
      </p>
      <table className="table table-striped table-hover">
        <thead className="table-dark">
          <tr>
            <th>Location</th>
            <th>Date</th>
            <th>Temp (C)</th>
            <th>Temp (F)</th>
            <th>Summary</th>
          </tr>
        </thead>
        <tbody>
          {forecasts.map(f => (
            <tr key={f.id}>
              <td>{f.location}</td>
              <td>{new Date(f.date).toLocaleDateString()}</td>
              <td>{f.temperatureC}</td>
              <td>{f.temperatureF}</td>
              <td>{f.summary ?? 'N/A'}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </>
  )
}
