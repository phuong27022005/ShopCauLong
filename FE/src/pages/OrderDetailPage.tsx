import { useCallback, useRef, useState } from 'react'
import { Link, useLocation, useParams } from 'react-router-dom'
import axios from 'axios'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'
import { useCatalogResource } from '../catalog/useCatalogResource'
import { LoadingSkeleton } from '../catalog/components/LoadingSkeleton'
import { ErrorState } from '../catalog/components/ErrorState'
import { formatVND } from '../catalog/formatVND'
import { OrderStatus, OrderTimeline } from '../orders/OrderStatus'
import { orderDate } from '../orders/types'
import type { Order } from '../orders/types'
import { PaymentBadge } from '../payments/PaymentBadge'
import { RetryPayment } from '../payments/RetryPayment'

export function OrderDetailPage({ success = false }: { success?: boolean }) {
  const { id } = useParams()
  const location = useLocation()
  const { api } = useAuth()
  const [confirmation, setConfirmation] = useState(false)
  const [sending, setSending] = useState(false)
  const locked = useRef(false)
  const [error, setError] = useState('')
  const [updated, setUpdated] = useState<Order | null>(null)
  const load = useCallback(async (signal: AbortSignal) => {
    const { data } = await api.get<ApiResponse<Order>>('/orders/' + id, { signal })
    if (!data.success) throw new Error(data.message)
    return data.data
  }, [api, id])
  const { state, retry } = useCatalogResource(load)
  if (state.status === 'loading') return <LoadingSkeleton message="Đang tải chi tiết đơn hàng…" count={1} />
  if (state.status === 'error') return axios.isAxiosError(state.error) && state.error.response?.status === 404
    ? <h1>Không tìm thấy đơn hàng</h1> : <ErrorState message="Không tải được đơn hàng." onRetry={retry} />
  const order = updated?.id === state.data.id ? updated : state.data
  async function cancel() {
    if (locked.current) return
    locked.current = true; setSending(true); setError('')
    try {
      const { data } = await api.post<ApiResponse<Order>>('/orders/' + id + '/cancel')
      if (!data.success) throw new Error(data.message)
      setUpdated(data.data); setConfirmation(false)
    } catch (cause) { setError(authErrorMessage(cause, 'Không hủy được đơn hàng.')); retry() }
    finally { locked.current = false; setSending(false) }
  }
  return <section className="space-y-5">
    <h1 className="text-3xl font-bold">{success ? 'Đặt hàng thành công' : 'Chi tiết đơn hàng'}</h1>
    <p className="font-bold">{order.code} · {orderDate(order.createdAt)}</p>
    <OrderStatus status={order.status} /><OrderTimeline status={order.status} />
    <PaymentBadge status={order.paymentStatus} />
    {typeof location.state?.paymentError === 'string' && <p role="alert" className="text-red-800">{location.state.paymentError}</p>}
    <RetryPayment key={order.id} order={order} />
    <section className="rounded border bg-white p-4">
      <h2 className="font-bold">Thông tin giao hàng</h2>
      <p>{order.fullName || 'Chưa có họ tên'}</p><p>{order.phone}</p><p>{order.shippingAddress}</p>
      <p>Thanh toán: {order.paymentMethod === 'VNPAY' ? 'VNPay' : 'COD'}</p>
      {order.note && <p>Ghi chú: {order.note}</p>}
    </section>
    <ul className="space-y-2">{order.items.map(item => <li key={item.productId} className="flex flex-wrap justify-between gap-2 border-b py-2">
      <span>{item.productName} × {item.quantity}</span><span>{formatVND(item.unitPrice)} / sản phẩm</span>
      <strong>{formatVND(item.lineTotal)}</strong>
    </li>)}</ul>
    <p className="text-xl font-bold">Tổng tiền: {formatVND(order.totalAmount)}</p>
    {error && <p role="alert" className="text-red-800">{error}</p>}
    {order.status === 'Pending' && !(order.paymentMethod === 'VNPAY' && order.paymentStatus === 'Paid') && <button onClick={() => setConfirmation(true)} className="rounded border border-red-500 px-4 py-2 text-red-800">Hủy đơn</button>}
    {confirmation && order.status === 'Pending' && !(order.paymentMethod === 'VNPAY' && order.paymentStatus === 'Paid') && <div role="dialog" aria-modal="true" aria-labelledby="cancel-title"
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 p-4">
      <div className="space-y-4 rounded bg-white p-6" onKeyDown={event => { if (event.key === 'Escape' && !sending) setConfirmation(false) }}>
        <h2 id="cancel-title" className="font-bold">Bạn có chắc muốn hủy đơn?</h2>
        <button autoFocus disabled={sending} onClick={() => setConfirmation(false)} className="mr-4 rounded border px-3 py-2">Giữ đơn</button>
        <button disabled={sending} onClick={() => void cancel()} className="rounded bg-red-800 px-3 py-2 text-white">
          {sending ? 'Đang hủy…' : 'Xác nhận hủy'}
        </button>
      </div>
    </div>}
    <Link to="/orders" className="block underline">Về danh sách đơn hàng</Link>
  </section>
}
