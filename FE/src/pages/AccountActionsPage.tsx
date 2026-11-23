import { useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import axios from 'axios'
import type { ApiResponse } from '../auth/types'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage, TOKEN_KEY } from '../auth/client'
import { registerSchema } from '../auth/schemas'
import { AuthCard, AuthSubmit } from '../components/AuthCard'

const client = axios.create({ baseURL: import.meta.env.VITE_API_BASE_URL || '/api', withCredentials: true })
export function AccountActionsPage({ action }: { action: 'forgot-password' | 'reset-password' | 'verify-email' | 'resend-verification' }) {
  const location = useLocation()
  const [token] = useState(() => new URLSearchParams(location.hash.slice(1)).get('token') || '')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [confirm, setConfirm] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const [message, setMessage] = useState('')
  const auth = useAuth()
  const reset = action === 'reset-password'
  const verify = action === 'verify-email'
  const title = reset ? 'Đặt lại mật khẩu' : verify ? 'Xác minh email' : action === 'forgot-password' ? 'Quên mật khẩu' : 'Gửi lại email xác minh'
  async function submit(event: React.FormEvent) {
    event.preventDefault(); setError('')
    if (reset) {
      const validation = registerSchema.safeParse({ fullName: 'Account', email: 'account@example.com', password, confirmPassword: confirm })
      if (!validation.success) { setError(validation.error.issues[0].message); return }
    }
    setBusy(true)
    try {
      const response = await client.post<ApiResponse<null>>('/auth/' + action, reset ? { token, password } : verify ? { token } : { email })
      setMessage(response.data.message)
      if (reset) {
        localStorage.removeItem(TOKEN_KEY)
        // A full navigation on the sign-in link below discards any in-memory old session.
      } else if (verify) auth.retrySession()
    } catch (cause) { setError(authErrorMessage(cause, 'Không thực hiện được. Vui lòng thử lại.')) }
    finally { setBusy(false) }
  }
  return <AuthCard title={title}>
    {message ? <div className="space-y-4"><p role="status" className="text-teal-800">{message}</p><a href="/login" className="underline">Về đăng nhập</a></div> : <form onSubmit={event => void submit(event)} className="space-y-4">
      {!reset && !verify && <><p>Nhập email tài khoản để nhận liên kết hướng dẫn.</p><label className="block">Email<input className="field" type="email" autoComplete="email" required maxLength={254} value={email} onChange={e => setEmail(e.target.value)} /></label></>}
      {(reset || verify) && !token && <p role="alert">Thiếu liên kết hợp lệ. Vui lòng mở liên kết trong email hoặc yêu cầu gửi lại.</p>}
      {reset && <><label className="block">Mật khẩu mới<input className="field" type="password" autoComplete="new-password" required maxLength={128} value={password} onChange={e => setPassword(e.target.value)} /></label><p className="text-sm text-slate-600">8–128 ký tự, gồm chữ hoa, chữ thường, số và ký tự đặc biệt.</p><label className="block">Xác nhận mật khẩu<input className="field" type="password" autoComplete="new-password" required value={confirm} onChange={e => setConfirm(e.target.value)} /></label></>}
      {verify && token && <p>Nhấn xác minh để xác nhận bạn sở hữu địa chỉ email này.</p>}
      {error && <p role="alert" className="text-red-800">{error}</p>}
      {(!(reset || verify) || token) && <AuthSubmit busy={busy} label={title} />}
      <Link to={reset ? '/forgot-password' : verify ? '/resend-verification' : '/login'} className="block underline">{reset || verify ? 'Yêu cầu liên kết mới' : 'Về đăng nhập'}</Link>
    </form>}
  </AuthCard>
}

export function VerificationStatus() {
  const { user } = useAuth()
  return <div className="mt-5 rounded-xl border bg-white p-4">
    <p>{user?.emailConfirmed ? '✓ Email đã được xác minh' : 'Email chưa được xác minh'}</p>
    {!user?.emailConfirmed && <Link to="/resend-verification" className="mt-2 inline-block text-teal-800 underline">Gửi email xác minh</Link>}
    <Link to="/forgot-password" className="mt-3 block underline">Đặt lại mật khẩu</Link>
  </div>
}
