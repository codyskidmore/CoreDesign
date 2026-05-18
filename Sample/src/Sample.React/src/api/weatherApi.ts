export interface WeatherForecast {
  id: string
  location: string
  date: string
  temperatureC: number
  temperatureF: number
  summary: string | null
}

export async function getWeatherForecasts(accessToken: string): Promise<WeatherForecast[]> {
  const response = await fetch('/api/weatherforecasts', {
    headers: { Authorization: `Bearer ${accessToken}` },
  })

  if (!response.ok)
    throw new Error(`${response.status} ${response.statusText}`)

  return response.json() as Promise<WeatherForecast[]>
}
