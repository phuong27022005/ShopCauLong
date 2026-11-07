import { useEffect, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'

export function useApiResource<T>(url: string | null) {
  const { api } = useAuth()
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<{ data?: T; error: string; loading: boolean }>({ error: '', loading: true })
  useEffect(() => {
    if (!url) { setState({ error: '', loading: false }); return }
    const controller = new AbortController()
    setState({ error: '', loading: true })
    void api.get<ApiResponse<T>>(url, { signal: controller.signal }).then(response => {
      if (!controller.signal.aborted) {
        if (!response.data.success) throw new Error(response.data.message)
        setState({ data: response.data.data, error: '', loading: false })
      }
    }).catch(cause => {
      if (!controller.signal.aborted) setState({ error: authErrorMessage(cause, 'Không tải được dữ liệu.'), loading: false })
    })
    return () => controller.abort()
  }, [url, api, attempt])
  return { ...state, reload: () => setAttempt(x => x + 1) }
}

