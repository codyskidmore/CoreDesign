export interface WeatherForecast {
  id: string
  location: string
  date: string
  temperatureC: number
  temperatureF: number
  summary: string | null
}

export async function getWeatherForecasts(accessToken: string): Promise<WeatherForecast[]> {
  const res = await fetch('/api/weatherforecasts', {
    headers: { Authorization: `Bearer ${accessToken}` },
  })
  if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
  return res.json() as Promise<WeatherForecast[]>
}
