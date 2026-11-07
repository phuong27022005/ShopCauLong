import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import type { ApiResponse } from '../auth/types'
import { authErrorMessage } from '../auth/client'
import type { Order } from '../orders/types'
import { PaymentBadge } from '../payments/PaymentBadge'
import { paymentDeadline } from '../payments/vnpay'

export function PaymentResultPage() {
  const [params] = useSearchParams()
  const rawId = params.get('orderId') ?? ''
  return <PaymentResult key={rawId} rawId={rawId} />
}
function PaymentResult({ rawId }: { rawId: string }) {
  const { api } = useAuth()
  const id = Number(rawId)
  const valid = /^\d+$/.test(rawId) && Number.isSafeInteger(id) && id > 0 && id <= 2147483647
  const [order, setOrder] = useState<Order | null>(null)
  const [error, setError] = useState('')
  const [finished, setFinished] = useState(false)
  useEffect(() => {
    if (!valid) return
    const controller = new AbortController()
    let timer: ReturnType<typeof setTimeout>
    let count = 0
    async function poll() {
      try {
        const { data } = await api.get<ApiResponse<Order>>('/orders/' + id, { signal: controller.signal })
        if (!data.success) throw new Error(data.message)
        if (controller.signal.aborted) return
        setOrder(data.data)
        count += 1
        const waiting = data.data.paymentMethod === 'VNPAY' && data.data.status === 'Pending' &&
          data.data.paymentStatus === 'Unpaid' && paymentDeadline(data.data.paymentExpiredAt) > Date.now()
        // Tối đa 6 lần đọc, cách nhau 3 giây; kết quả URL không quyết định trạng thái.
        if (waiting && count < 6) timer = setTimeout(() => void poll(), 3000)
        else setFinished(true)
      } catch (cause) {
        if (!controller.signal.aborted) { setError(authErrorMessage(cause, 'Không kiểm tra được thanh toán.')); setFinished(true) }
      }
    }
    void poll()
    return () => { controller.abort(); clearTimeout(timer) }
  }, [api, id, valid])
  const title = order?.paymentStatus === 'Paid' ? 'Thanh toán thành công'
    : order?.paymentStatus === 'Failed' ? 'Thanh toán thất bại'
    : order?.paymentStatus === 'Refunded' ? 'Đã hoàn tiền'
    : order && !finished ? 'Đang xử lý thanh toán' : 'Chưa xác nhận thanh toán'
  return <section className="space-y-4">
    <h1 className="text-3xl font-bold">{title}</h1>
    {!valid && <p role="alert">Không có mã đơn hợp lệ. Vui lòng xem danh sách đơn hàng.</p>}
    {valid && !order && !error && <p role="status">Đang kiểm tra trạng thái từ server…</p>}
    {order && <>
      <p>{order.code}</p><PaymentBadge status={order.paymentStatus} />
      {order.paymentStatus === 'Unpaid' && <p>{finished
        ? 'Chưa nhận được xác nhận thanh toán. Hãy kiểm tra lại trong chi tiết đơn hàng.'
        : 'Đang chờ xác nhận từ VNPay. Trang sẽ tự kiểm tra lại.'}</p>}
    </>}
    {error && <p role="alert" className="text-red-800">{error}</p>}
    {valid && <Link to={'/orders/' + id} className="block underline">Xem chi tiết đơn hàng</Link>}
    <Link to="/orders" className="block underline">Về danh sách đơn hàng</Link>
  </section>
}
