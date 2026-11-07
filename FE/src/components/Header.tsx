import { Link } from 'react-router-dom'
import { useCart } from '../cart/CartContext'
import { useAuth } from '../auth/AuthContext'

export function Header() {
  const cart = useCart()
  const count = cart.items.reduce((sum, item) => sum + item.quantity, 0)
  const { user, status, logout } = useAuth()

  return (
    <header className="border-b border-slate-200 bg-white">
      <div className="mx-auto flex max-w-5xl flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-6">
        <Link to="/" className="text-lg font-bold text-teal-900">Badminton Shop</Link>
        <nav aria-label="Điều hướng chính" className="flex flex-wrap items-center gap-x-4 gap-y-2 text-sm">
          <Link to="/products" className="underline">Sản phẩm</Link>
          <Link to="/cart" aria-label={`Giỏ hàng, ${count} sản phẩm`} className="rounded border px-3 py-2"><span aria-hidden="true">🛒</span> <span>{count}</span></Link>
          {user ? (
            <>
              <span className="max-w-56 break-words font-medium">{user.fullName}</span>
              {user.role === 'Admin' && <Link to="/admin" className="underline">Quản trị</Link>}
              <Link to="/account" className="underline">Tài khoản</Link>
              <Link to="/orders" className="underline">Đơn hàng</Link>
              <button type="button" className="rounded-md border px-3 py-2" onClick={() => void logout()}>
                Đăng xuất
              </button>
            </>
          ) : status === 'ready' && (
            <>
              <Link to="/login" className="underline">Đăng nhập</Link>
              <Link to="/register" className="rounded-md border px-3 py-2">Đăng ký</Link>
            </>
          )}
        </nav>
      </div>
    </header>
  )
}
