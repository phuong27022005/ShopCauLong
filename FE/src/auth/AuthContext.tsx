import { createContext, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import axios from 'axios'
import { createAuthClient, TOKEN_KEY } from './client'
import type { User } from './types'

type AuthClient = ReturnType<typeof createAuthClient>
type SessionStatus = 'checking' | 'ready' | 'error'

interface AuthContextValue {
  api: AuthClient['api']
  user: User | null
  token: string | null
  status: SessionStatus
  retrySession: () => void
  login: (input: Parameters<AuthClient['login']>[0]) => Promise<void>
  register: AuthClient['register']
  logout: () => Promise<void>
}

const AuthContext = createContext<AuthContextValue | null>(null)

export function AuthProvider({ children }: { children: ReactNode }) {
  const navigate = useNavigate()
  const [token, setToken] = useState(() => localStorage.getItem(TOKEN_KEY))
  const [user, setUser] = useState<User | null>(null)
  const [status, setStatus] = useState<SessionStatus>(token ? 'checking' : 'ready')
  const [redirectToLogin, setRedirectToLogin] = useState(false)
  const [checkAttempt, setCheckAttempt] = useState(0)
  const [client] = useState(() => createAuthClient(setToken, () => {
    setUser(null)
    setStatus('ready')
    setRedirectToLogin(true)
  }))

  useEffect(() => {
    if (!client.hasToken()) return

    // Hủy /me khi provider bị tháo hoặc lượt kiểm tra thay đổi.
    const controller = new AbortController()
    void client.me(controller.signal).then((profile) => {
      if (controller.signal.aborted || !client.hasToken()) return
      setUser(profile)
      setStatus('ready')
    }).catch((error: unknown) => {
      if (!axios.isCancel(error) && !controller.signal.aborted && client.hasToken()) setStatus('error')
    })

    return () => controller.abort()
  }, [client, checkAttempt])

  useEffect(() => {
    if (redirectToLogin) {
      void navigate('/login', { replace: true })
      setRedirectToLogin(false)
    }
  }, [navigate, redirectToLogin])

  async function logout() {
    setUser(null)
    setStatus('ready')
    setRedirectToLogin(true)
    try {
      await client.logout()
    } catch {
      // Trạng thái cục bộ đã được xóa dù máy chủ không phản hồi.
    }
  }

  return (
    <AuthContext.Provider value={{
      api: client.api,
      user,
      token,
      status,
      retrySession: () => {
        setStatus('checking')
        setCheckAttempt((attempt) => attempt + 1)
      },
      login: async (input) => {
        setUser(await client.login(input))
        setStatus('ready')
      },
      register: client.register,
      logout,
    }}>
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (!context) throw new Error('useAuth cần nằm trong AuthProvider.')
  return context
}
