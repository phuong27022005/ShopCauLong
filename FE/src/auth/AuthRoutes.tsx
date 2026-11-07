import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuth } from './AuthContext'

export function SessionStatus() {
  const { status, retrySession } = useAuth()

  if (status === 'checking') return <p role="status">Đang kiểm tra phiên đăng nhập…</p>
  if (status === 'error') {
    return (
      <div>
        <p role="alert">Không thể kiểm tra phiên đăng nhập.</p>
        <button type="button" onClick={retrySession} className="mt-2 underline">Thử lại</button>
      </div>
    )
  }
  return null
}

export function ProtectedRoute() {
  const { user, status } = useAuth()
  const { pathname, search, hash } = useLocation()

  if (status !== 'ready') return <SessionStatus />
  return user
    ? <Outlet />
    : <Navigate to="/login" replace state={{ returnTo: pathname + search + hash }} />
}

export function AdminRoute() {
  const { user } = useAuth()

  return user?.role === 'Admin' ? <Outlet /> : <Navigate to="/forbidden" replace />
}
