import { useState } from 'react'
import { useCoupon } from './CouponContext'
import { useCart } from '../cart/CartContext'
import { formatVND } from '../catalog/formatVND'

export function CouponBox() {
  const coupon = useCoupon()
  const cart = useCart()
  const [input, setInput] = useState(coupon.code)
  return <section className="space-y-3 rounded border bg-white p-4" aria-label="Ưu đãi">
    <label className="block">Mã giảm giá<input value={input} maxLength={50} onChange={e => setInput(e.target.value)} className="ml-2 rounded border p-2" /></label>
    <button type="button" disabled={coupon.busy || cart.busy} onClick={() => void coupon.apply(input)} className="rounded border px-3 py-2">
      {coupon.busy ? 'Đang áp dụng…' : 'Áp dụng'}
    </button>
    {coupon.error && <p role="alert">{coupon.error}</p>}
    {coupon.code && <p>Đã áp dụng {coupon.code} <button type="button" onClick={() => { coupon.clear(); setInput('') }} className="underline">Xóa mã</button></p>}
    <p>Tạm tính: {formatVND(cart.totalAmount)}</p>
    {coupon.code && <p>Giảm giá: −{formatVND(coupon.discount)}</p>}
    <p className="font-bold">Tổng tiền: {formatVND(coupon.total)}</p>
    <p className="text-sm text-slate-600">Giá trị mã được kiểm tra lại khi đặt hàng. Nếu thay đổi giỏ, hãy áp dụng lại mã.</p>
  </section>
}

