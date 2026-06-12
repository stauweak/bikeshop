import { useCallback, useEffect, useState } from 'react'
import { apiGet } from '../api/client'

/** Charge une ressource GET et expose un reload pour rafraîchir après mutation. */
export function useApi<T>(url: string) {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)

  const reload = useCallback(() => {
    setLoading(true)
    apiGet<T>(url)
      .then((d) => {
        setData(d)
        setError(null)
      })
      .catch((e: Error) => setError(e.message))
      .finally(() => setLoading(false))
  }, [url])

  useEffect(reload, [reload])

  return { data, error, loading, reload }
}
