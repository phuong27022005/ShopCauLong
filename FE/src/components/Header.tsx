import { Link, NavLink } from 'react-router-dom'
import { useCart } from '../cart/CartContext'
import { useAuth } from '../auth/AuthContext'

function NavIcon({ type }: { type: 'products' | 'compare' | 'cart' }) {
  return <svg aria-hidden="true" width="19" height="19" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.7" strokeLinecap="round" strokeLinejoin="round">
    {type === 'products' ? <><rect x="3" y="3" width="7" height="7" rx="2" /><rect x="14" y="3" width="7" height="7" rx="2" /><rect x="3" y="14" width="7" height="7" rx="2" /><rect x="14" y="14" width="7" height="7" rx="2" /></> : type === 'compare' ? <><path d="M8 4v16M16 4v16M4 8h8M12 16h8" /><circle cx="8" cy="8" r="2" fill="white" /><circle cx="16" cy="16" r="2" fill="white" /></> : <><path d="M2 3h3l3 12h11l3-9H6M9 18h10" /><circle cx="9" cy="21" r="1" /><circle cx="19" cy="21" r="1" /></>}
  </svg>
}

const navClass = ({ isActive }: { isActive: boolean }) => 'shop-nav-link' + (isActive ? ' is-current' : '')

export function Header() {
  const cart = useCart()
  const count = cart.items.reduce((sum, item) => sum + item.quantity, 0)
  const { user, status, logout } = useAuth()

  return (
    <header className="shop-header">
      <div className="shop-header-inner">
        <Link to="/" className="shop-brand" aria-label="Badminton Shop">
          <span className="shop-brand-mark" aria-hidden="true"><svg width="26" height="26" viewBox="0 0 30 30" fill="none"><g transform="rotate(35 15 15)" stroke="currentColor" strokeWidth="1.7"><ellipse cx="15" cy="10" rx="7" ry="8" /><path d="M15 18v9M11 4v12m4-14v16m4-14v12M9 7h12M8 11h14M10 15h10" /></g></svg></span>
          <span><span className="block">Badminton<span className="font-normal"> Shop</span></span><span className="shop-brand-tagline">CHỌN VỢT · CHỌN ĐAM MÊ</span></span>
        </Link>
        <nav aria-label="Điều hướng chính" className="shop-navigation">
          <div className="shop-primary-links">
            <NavLink to="/products" className={navClass}><NavIcon type="products" />Sản phẩm</NavLink>
            <NavLink to="/compare" className={navClass}><NavIcon type="compare" />So sánh</NavLink>
            <NavLink to="/cart" aria-label={`Giỏ hàng, ${count} sản phẩm`} className={({ isActive }) => navClass({ isActive }) + ' shop-cart-link'}><NavIcon type="cart" /><span>Giỏ hàng</span><span className="shop-cart-count">{count > 99 ? '99+' : count}</span></NavLink>
          </div>
          {user ? (
            <div className="shop-member-links">
              <span className="shop-member-name"><span aria-hidden="true" className="shop-avatar">{user.fullName.trim().charAt(0).toUpperCase()}</span><span className="truncate">{user.fullName}</span></span>
              {user.role === 'Admin' && <NavLink to="/admin" className={navClass}>Quản trị</NavLink>}
              <NavLink to="/account" className={navClass}>Tài khoản</NavLink>
              <NavLink to="/favorites" className={navClass}>Yêu thích</NavLink>
              <NavLink to="/orders" className={navClass}>Đơn hàng</NavLink>
              <button type="button" className="shop-signout" onClick={() => void logout()}>
                Đăng xuất
              </button>
            </div>
          ) : status === 'ready' && (
            <div className="shop-auth-links">
              <NavLink to="/login" className="shop-login">Đăng nhập</NavLink>
              <NavLink to="/register" className="shop-register">Đăng ký</NavLink>
            </div>
          )}
        </nav>
      </div>
    </header>
  )
}
