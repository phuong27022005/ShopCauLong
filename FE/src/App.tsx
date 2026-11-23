import { CouponProvider } from './coupons/CouponContext'
import { AdminLayout } from './admin/AdminLayout'
import { lazy, Suspense } from 'react'
const Dashboard = lazy(() => import('./admin/Dashboard').then(module => ({ default: module.Dashboard })))
import { AdminProducts } from './admin/AdminProducts'
import { AdminOrders } from './admin/AdminOrders'
import { AdminCoupons } from './admin/AdminCoupons'
import { AdminReviews } from './admin/AdminReviews'
import { AdminInventory } from './admin/AdminInventory'
import { Route, Routes } from 'react-router-dom'
import { AuthProvider, useAuth } from './auth/AuthContext'
import { AdminRoute, ProtectedRoute } from './auth/AuthRoutes'
import { Header } from './components/Header'
import { CartProvider } from './cart/CartContext'
import { CartPage } from './pages/CartPage'
import { CheckoutPage } from './pages/CheckoutPage'
import { OrdersPage } from './pages/OrdersPage'
import { OrderDetailPage } from './pages/OrderDetailPage'
import { PaymentResultPage } from './pages/PaymentResultPage'
import { HomePage } from './pages/HomePage'
import { LoginPage } from './pages/LoginPage'
import { ProductDetailPage } from './pages/ProductDetailPage'
import { ProductsPage } from './pages/ProductsPage'
import { RegisterPage } from './pages/RegisterPage'
import { AccountActionsPage, VerificationStatus } from './pages/AccountActionsPage'
import { ShoppingProvider, ShoppingNotice, FavoritesPage, ComparePage } from './shopping/ShoppingTools'

function AccountPage() {
  const { user } = useAuth()
  return (
    <section>
      <h1 className="text-2xl font-bold">Tài khoản của tôi</h1>
      <p className="mt-2">{user?.email}</p>
      <VerificationStatus />
    </section>
  )
}

export function App() {
  return (
    <AuthProvider>
      <CartProvider><CouponProvider><ShoppingProvider>
        <div className="min-h-screen bg-slate-50 text-slate-900">
          <Header />
          <main className="mx-auto max-w-5xl px-4 py-8 sm:px-6 sm:py-12">
            <ShoppingNotice />
            <Routes>
              <Route path="/" element={<HomePage />} />
              <Route path="/products" element={<ProductsPage />} />
              <Route path="/products/:slug" element={<ProductDetailPage />} />
              <Route path="/cart" element={<CartPage />} />
              <Route path="/login" element={<LoginPage />} />
              <Route path="/register" element={<RegisterPage />} />
              <Route path="/compare" element={<ComparePage />} />
              {(['forgot-password', 'reset-password', 'verify-email', 'resend-verification'] as const).map(action =>
                <Route key={action} path={'/' + action} element={<AccountActionsPage key={action} action={action} />} />)}
              <Route element={<ProtectedRoute />}>
                <Route path="/favorites" element={<FavoritesPage />} />
                <Route path="/checkout" element={<CheckoutPage />} />
                <Route path="/payment/result" element={<PaymentResultPage />} />
                <Route path="/orders" element={<OrdersPage />} />
                <Route path="/orders/:id" element={<OrderDetailPage />} />
                <Route path="/orders/:id/success" element={<OrderDetailPage success />} />
                <Route path="/account" element={<AccountPage />} />
                <Route element={<AdminRoute />}>
                  <Route path="/admin" element={<AdminLayout />}>
                    <Route index element={<Suspense fallback={<p role="status">Đang tải dashboard…</p>}><Dashboard /></Suspense>} />
                    <Route path="products" element={<AdminProducts />} />
                    <Route path="inventory" element={<AdminInventory />} />
                    <Route path="orders" element={<AdminOrders />} />
                    <Route path="coupons" element={<AdminCoupons />} />
                    <Route path="reviews" element={<AdminReviews />} />
                  </Route>
                </Route>
              </Route>
              <Route path="/forbidden" element={<h1 className="text-2xl font-bold">Bạn không có quyền truy cập</h1>} />
              <Route path="*" element={<h1 className="text-2xl font-bold">Không tìm thấy trang</h1>} />
            </Routes>
          </main>
        </div>
      </ShoppingProvider></CouponProvider></CartProvider>
    </AuthProvider>
  )
}
