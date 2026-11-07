import { CouponBox } from '../coupons/CouponBox'
import { useCoupon } from '../coupons/CouponContext'
import { useRef } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'
import { useCart } from '../cart/CartContext'
import { AuthField } from '../components/AuthField'
import { formatVND } from '../catalog/formatVND'
import type { Order } from '../orders/types'
import { startVnpay } from '../payments/vnpay'

export const checkoutSchema = z.object({
  fullName: z.string().trim().min(1, 'Vui lòng nhập họ tên.').max(150),
  phone: z.string().trim().regex(/^(?:0|\+84)(?:[35789]\d{8}|2\d{9})$/, 'Số điện thoại Việt Nam không hợp lệ.'),
  shippingAddress: z.string().trim().min(1, 'Vui lòng nhập địa chỉ.').max(500),
  note: z.string().trim().max(1000, 'Ghi chú tối đa 1000 ký tự.'),
  paymentMethod: z.enum(['COD', 'VNPAY']),
})
type Form = z.infer<typeof checkoutSchema>

export function CheckoutPage() {
  const { user, api } = useAuth()
  const cart = useCart()
  const coupon = useCoupon()
  const navigate = useNavigate()
  const sending = useRef(false)
  const { register, handleSubmit, setError, formState: { errors, isSubmitting } } = useForm<Form>({
    resolver: zodResolver(checkoutSchema),
    defaultValues: { fullName: user?.fullName ?? '', phone: '', shippingAddress: '', note: '', paymentMethod: 'COD' },
  })
  const blocked = coupon.busy || cart.busy || !cart.items.length || cart.items.some(x => !x.isAvailable)
  async function submit(input: Form) {
    if (sending.current || blocked) return
    sending.current = true
    try {
      const response = await api.post<ApiResponse<Order>>('/orders', { ...input, ...(coupon.code ? { couponCode: coupon.code } : {}) })
      if (!response.data.success) throw new Error(response.data.message)
      cart.completeOrder()
      coupon.clear()
      if (input.paymentMethod === 'VNPAY') {
        try { await startVnpay(api, response.data.data.id) }
        catch (cause) {
          void navigate('/orders/' + response.data.data.id, {
            replace: true, state: { paymentError: authErrorMessage(cause, 'Đơn đã tạo nhưng chưa mở được thanh toán. Vui lòng thanh toán lại.') },
          })
        }
        return
      }
      void navigate('/orders/' + response.data.data.id + '/success', { replace: true })
    } catch (error) {
      setError('root', { message: authErrorMessage(error, 'Không tạo được đơn hàng. Vui lòng kiểm tra lại giỏ.') })
      sending.current = false
    }
  }
  return <section className="space-y-6">
    <h1 className="text-3xl font-bold">Thanh toán</h1>
    {cart.busy && <p role="status">Đang tải giỏ hàng…</p>}
    {!cart.busy && !cart.items.length && <p>Giỏ hàng đang trống. <Link to="/products" className="underline">Mua sắm</Link></p>}
    <div className="grid gap-6 md:grid-cols-2">
      <form noValidate onSubmit={handleSubmit(submit)} className="space-y-4">
        <AuthField label="Họ tên người nhận" autoComplete="name" field={register('fullName')} error={errors.fullName?.message} />
        <AuthField label="Số điện thoại" type="tel" autoComplete="tel" field={register('phone')} error={errors.phone?.message} />
        <AuthField label="Địa chỉ giao hàng" autoComplete="street-address" field={register('shippingAddress')} error={errors.shippingAddress?.message} />
        <label className="block">Ghi chú<textarea {...register('note')} className="mt-1 w-full rounded border p-2" /></label>
        {errors.note && <p role="alert">{errors.note.message}</p>}
        <fieldset className="space-y-2"><legend>Phương thức thanh toán</legend>
          <label className="block"><input type="radio" value="COD" {...register('paymentMethod')} /> Thanh toán khi nhận hàng (COD)</label>
          <label className="block"><input type="radio" value="VNPAY" {...register('paymentMethod')} /> VNPay</label>
        </fieldset>
        {errors.root && <div role="alert" className="rounded bg-red-50 p-3 text-red-900">
          <p>{errors.root.message}</p><Link to="/cart" className="underline">Về giỏ hàng để kiểm tra</Link>
        </div>}
        <button disabled={blocked || isSubmitting} className="rounded bg-teal-800 px-5 py-3 text-white disabled:opacity-40">
          {isSubmitting ? 'Đang đặt hàng…' : 'Đặt hàng'}
        </button>
      </form>
      <aside className="space-y-3 rounded border bg-white p-5">
        <h2 className="text-xl font-bold">Tóm tắt giỏ hàng</h2>
        {cart.items.map(item => <div key={item.productId} className="border-b py-2">
          <p>{item.product.name} × {item.quantity}</p><p>{formatVND(item.lineTotal)}</p>
          {!item.isAvailable && <p className="text-red-800">{item.product.name} không đủ hàng.</p>}
        </div>)}
        <CouponBox />
        <Link to="/cart" className="inline-block underline">Chỉnh sửa giỏ hàng</Link>
      </aside>
    </div>
  </section>
}
