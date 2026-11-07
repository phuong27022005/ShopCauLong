import { CouponBox } from '../coupons/CouponBox'
import { useCoupon } from '../coupons/CouponContext'
import { Link, useNavigate } from 'react-router-dom'
import { useCart } from '../cart/CartContext'
import { EmptyState } from '../catalog/components/EmptyState'
import { formatVND } from '../catalog/formatVND'
import { CatalogImage } from '../catalog/components/CatalogImage'

export function CartPage() {
  const cart = useCart()
  const coupon = useCoupon()
  const navigate = useNavigate()
  const blocked = coupon.busy || cart.busy || cart.items.some(x => !x.isAvailable || x.quantity > x.product.stock)
  return <section className="space-y-5">
    <h1 className="text-3xl font-bold">Giỏ hàng</h1>
    {cart.busy && <p role="status">Đang cập nhật giỏ hàng…</p>}
    {cart.error && cart.busy && <button onClick={cart.retry} className="underline">Tải lại giỏ hàng</button>}
    {!cart.items.length ? <><EmptyState message="Giỏ hàng đang trống." /><Link to="/products" className="underline">Tiếp tục mua sắm</Link></> : <>
      <ul className="space-y-4">
        {cart.items.map(item => <li key={item.productId} className="flex flex-wrap items-center gap-4 rounded border bg-white p-4">
          <CatalogImage src={item.product.imageUrl} alt="" className="size-20 object-contain" />
          <div className="min-w-40 flex-1">
            <Link to={'/products/' + item.product.slug} className="font-semibold">{item.product.name}</Link>
            <p>{formatVND(item.unitPrice)}</p>
            {!item.isAvailable && <p className="text-red-800">Sản phẩm hết hàng, đã ẩn hoặc số lượng vượt tồn kho.</p>}
          </div>
          <div className="flex items-center gap-2" aria-label={'Số lượng ' + item.product.name}>
            <button aria-label={'Giảm ' + item.product.name} disabled={cart.busy && cart.pendingId !== item.productId}
              onClick={() => cart.update(item.productId, item.quantity - 1)} className="rounded border px-3 py-2">−</button>
            <span>{item.quantity}</span>
            <button aria-label={'Tăng ' + item.product.name} disabled={cart.busy && cart.pendingId !== item.productId}
              onClick={() => cart.update(item.productId, item.quantity + 1)} className="rounded border px-3 py-2">+</button>
          </div>
          <strong>{formatVND(item.lineTotal)}</strong>
          <button disabled={cart.busy} onClick={() => void cart.remove(item.productId)} className="underline">Xóa {item.product.name}</button>
        </li>)}
      </ul>
      <CouponBox />
      <button disabled={blocked} onClick={() => navigate('/checkout')} className="rounded bg-teal-800 px-5 py-3 text-white disabled:opacity-40">Thanh toán</button>
    </>}
  </section>
}
