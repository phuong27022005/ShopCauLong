import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'
import { orderDate } from './types'

type Entry = { id: number; actorName: string; event: string; fromValue: string | null; toValue: string; createdAt: string }
const labels: Record<string, string> = { Pending: 'Chờ xác nhận', Confirmed: 'Đã xác nhận', Shipping: 'Đang giao', Completed: 'Hoàn thành', Cancelled: 'Đã hủy', Paid: 'Đã thanh toán', Unpaid: 'Chưa thanh toán', Failed: 'Thanh toán thất bại' }
export function OrderHistory({ id, admin = false }: { id: number; admin?: boolean }) {
  const result = useApiResource<Entry[]>(`${admin ? '/admin' : ''}/orders/${id}/history`)
  return <section className="space-y-3 rounded-xl border bg-white p-4"><h2 className="font-bold">Lịch sử xử lý đơn hàng</h2><ResourceState {...result} />
    {result.data?.length === 0 && <p className="text-sm text-slate-600">Chưa có lịch sử được ghi nhận cho đơn này.</p>}
    <ol className="space-y-4 border-l-2 border-teal-200 pl-4">{result.data?.map(entry => <li key={entry.id}>
      <p className="font-medium">{entry.fromValue ? `${labels[entry.fromValue] ?? entry.fromValue} → ` : 'Tạo đơn · '}{labels[entry.toValue] ?? entry.toValue}</p>
      <p className="text-sm text-slate-600">{entry.actorName} · {orderDate(entry.createdAt)}</p>
    </li>)}</ol>
  </section>
}
