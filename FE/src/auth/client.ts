import axios from 'axios'
import type { AxiosRequestConfig } from 'axios'
import type { ApiResponse, AuthSession, User } from './types'
import type { LoginInput, RegisterInput } from './schemas'

export const TOKEN_KEY = 'badminton-shop.access-token'

declare module 'axios' {
  interface AxiosRequestConfig {
    authAttempt?: { session: number; revision: number; retried: boolean }
  }
}

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) throw new Error(response.message)
  return response.data
}

export function authErrorMessage(error: unknown, fallback: string): string {
  if (axios.isAxiosError<ApiResponse<unknown>>(error)) {
    const message = error.response?.data?.message
    return typeof message === 'string' && message.trim() ? message : fallback
  }
  return error instanceof Error && error.message ? error.message : fallback
}

export function createAuthClient(onToken: (token: string | null) => void, onExpired: () => void) {
  const options = { baseURL: import.meta.env.VITE_API_BASE_URL || '/api', withCredentials: true }
  const api = axios.create(options)
  const auth = axios.create(options)
  let token = localStorage.getItem(TOKEN_KEY)
  let session = 0
  let revision = 0
  let pendingRefresh: Promise<void> | undefined

  function storeToken(value: string | null) {
    token = value
    revision += 1
    if (value) localStorage.setItem(TOKEN_KEY, value)
    else localStorage.removeItem(TOKEN_KEY)
    onToken(value)
  }

  function clearSession() {
    session += 1
    pendingRefresh = undefined
    storeToken(null)
  }

  function expireSession() {
    clearSession()
    onExpired()
  }

  function refresh(expectedSession: number) {
    // Các yêu cầu 401 đồng thời dùng chung một lượt refresh.
    pendingRefresh ??= auth.post<ApiResponse<{ accessToken: string }>>('/auth/refresh')
      .then(({ data }) => {
        const { accessToken } = unwrap(data)
        if (!accessToken) throw new Error('API không trả về token mới.')
        if (session === expectedSession) storeToken(accessToken)
      })
      .catch((error: unknown) => {
        if (session === expectedSession) expireSession()
        throw error
      })
      .finally(() => {
        if (session === expectedSession) pendingRefresh = undefined
      })

    return pendingRefresh
  }

  api.interceptors.request.use((config) => {
    if (config.authAttempt && config.authAttempt.session !== session) {
      throw new axios.CanceledError('Phiên đăng nhập đã thay đổi.')
    }
    if (token) config.headers.set('Authorization', `Bearer ${token}`)
    config.authAttempt = { session, revision, retried: config.authAttempt?.retried ?? false }
    return config
  })

  api.interceptors.response.use(undefined, async (error: unknown) => {
    if (!axios.isAxiosError(error) || error.response?.status !== 401) throw error
    const config: AxiosRequestConfig | undefined = error.config
    const attempt = config?.authAttempt
    if (!config || !attempt || !token || attempt.session !== session || config.signal?.aborted) throw error

    if (attempt.retried) {
      if (attempt.revision === revision) expireSession()
      throw error
    }

    if (attempt.revision === revision) await refresh(attempt.session)
    if (attempt.session !== session || config.signal?.aborted) throw error

    attempt.retried = true
    return api.request(config)
  })

  return {
    api,
    hasToken: () => Boolean(token),
    async me(signal: AbortSignal) {
      const current = session
      const response = await api.get<ApiResponse<User>>('/auth/me', { signal })
      if (current !== session) throw new axios.CanceledError('Phiên đăng nhập đã thay đổi.')
      return unwrap(response.data)
    },
    async login(input: LoginInput) {
      const current = session
      const result = unwrap((await auth.post<ApiResponse<AuthSession>>('/auth/login', input)).data)
      if (current !== session) throw new axios.CanceledError('Phiên đăng nhập đã thay đổi.')
      if (!result.accessToken || !result.user) throw new Error('API trả về phiên đăng nhập không hợp lệ.')
      session += 1
      pendingRefresh = undefined
      storeToken(result.accessToken)
      return result.user
    },
    async register({ fullName, email, password }: RegisterInput) {
      unwrap((await auth.post<ApiResponse<null>>('/auth/register', { fullName, email, password })).data)
    },
    async logout() {
      const previousToken = token
      clearSession()
      await auth.post('/auth/logout', undefined, {
        headers: previousToken ? { Authorization: `Bearer ${previousToken}` } : undefined,
      })
    },
  }
}
