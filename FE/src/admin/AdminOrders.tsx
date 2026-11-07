import { useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'
import type { Order, OrderPage } from '../orders/types'
import { OrderStatus } from '../orders/OrderStatus'
import { PaymentBadge } from '../payments/PaymentBadge'
import { formatVND } from '../catalog/formatVND'
import { Pagination } from '../catalog/components/Pagination'
import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'

export function nextStatuses(order: Order) {
  const next: Record<string, string[]> = { Pending: ['Confirmed', 'Cancelled'], Confirmed: ['Shipping', 'Cancelled'], Shipping: ['Completed'] }
  return (next[order.status] ?? []).filter(x => x !== 'Cancelled' || order.paymentMethod !== 'VNPAY' || order.paymentStatus !== 'Paid')
}
function Transition({ order, saved }: { order: Order; saved: () => void }) {
  const { api } = useAuth()
  const [status, setStatus] = useState('')
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const options = nextStatuses(order)
  if (!options.length) return <span>Không có chuyển đổi</span>
  async function update() {
    if (!status || busy) return
    setBusy(true); setError('')
    try {
      const response = await api.put<ApiResponse<Order>>('/admin/orders/' + order.id + '/status', { status })
      if (!response.data.success) throw new Error(response.data.message)
      saved()
    } catch (cause) { setError(authErrorMessage(cause, 'Không đổi được trạng thái.')) }
    finally { setBusy(false) }
  }
  return <div className="space-y-2"><select aria-label={'Trạng thái mới ' + order.code} value={status} onChange={e => setStatus(e.target.value)} className="rounded border p-2">
    <option value="">Chọn bước tiếp</option>{options.map(x => <option key={x}>{x}</option>)}</select>
    <button disabled={!status || busy} onClick={() => void update()} className="rounded border p-2">Cập nhật</button>
    {error && <p role="alert">{error}</p>}</div>
}
export function AdminOrders() {
  const [status, setStatus] = useState('')
  const [page, setPage] = useState(1)
  const result = useApiResource<OrderPage>(`/admin/orders?page=${page}&pageSize=12${status ? '&status=' + status : ''}`)
  return <>
    <h2 className="text-2xl font-bold">Quản lý đơn hàng</h2>
    <label>Lọc trạng thái<select className="ml-2 rounded border p-2" value={status} onChange={e => { setStatus(e.target.value); setPage(1) }}>
      <option value="">Tất cả</option>{['Pending', 'Confirmed', 'Shipping', 'Completed', 'Cancelled'].map(x => <option key={x}>{x}</option>)}</select></label>
    <ResourceState {...result} />
    {result.data && <>
      {!result.data.items.length ? <p>Chưa có đơn hàng.</p> : <div className="overflow-x-auto"><table className="w-full"><thead><tr><th>Mã đơn</th><th>Tổng tiền</th><th>Trạng thái</th><th>Thao tác</th></tr></thead><tbody>
        {result.data.items.map(order => <tr key={order.id} className="border-b"><td className="p-3">{order.code}<p>{order.fullName} · {order.phone}</p></td>
          <td>{formatVND(order.totalAmount)}</td><td><OrderStatus status={order.status} /><PaymentBadge status={order.paymentStatus} /></td>
          <td className="p-3"><Transition key={order.status} order={order} saved={result.reload} /></td></tr>)}
      </tbody></table></div>}
      <Pagination label="Phân trang đơn hàng" page={page} totalPages={result.data.totalPages} onChange={setPage} />
    </>}
  </>
}

