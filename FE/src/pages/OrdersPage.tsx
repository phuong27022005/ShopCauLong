import { useCallback } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import type { ApiResponse } from '../auth/types'
import { useCatalogResource } from '../catalog/useCatalogResource'
import { LoadingSkeleton } from '../catalog/components/LoadingSkeleton'
import { EmptyState } from '../catalog/components/EmptyState'
import { ErrorState } from '../catalog/components/ErrorState'
import { Pagination } from '../catalog/components/Pagination'
import { formatVND } from '../catalog/formatVND'
import { OrderStatus } from '../orders/OrderStatus'
import { PaymentBadge } from '../payments/PaymentBadge'
import { orderDate } from '../orders/types'
import type { OrderPage } from '../orders/types'

export function OrdersPage() {
  const { api } = useAuth()
  const [params, setParams] = useSearchParams()
  const value = Number(params.get('page') ?? 1)
  const page = Number.isInteger(value) && value > 0 && value <= 2147483647 ? value : 1
  const load = useCallback(async (signal: AbortSignal) => {
    const { data } = await api.get<ApiResponse<OrderPage>>('/orders', { signal, params: { page, pageSize: 12 } })
    if (!data.success) throw new Error(data.message)
    return data.data
  }, [api, page])
  const { state, retry } = useCatalogResource(load)
  return <section className="space-y-5">
    <h1 className="text-3xl font-bold">Đơn hàng của tôi</h1>
    {state.status === 'loading' && <LoadingSkeleton message="Đang tải đơn hàng…" />}
    {state.status === 'error' && <ErrorState message="Không tải được đơn hàng." onRetry={retry} />}
    {state.status === 'success' && <>
      {!state.data.items.length ? <EmptyState message="Chưa có đơn hàng trên trang này." /> :
        <ul className="space-y-3">{state.data.items.map(order => <li key={order.id} className="flex flex-wrap items-center justify-between gap-3 rounded border bg-white p-4">
          <Link to={'/orders/' + order.id} className="font-bold underline">{order.code}</Link>
          <time dateTime={order.createdAt}>{orderDate(order.createdAt)}</time>
          <span>{formatVND(order.totalAmount)}</span><OrderStatus status={order.status} /><PaymentBadge status={order.paymentStatus} />
        </li>)}</ul>}
      <Pagination page={page} totalPages={state.data.totalPages} label="Phân trang đơn hàng"
        onChange={next => setParams({ page: String(next) })} />
    </>}
  </section>
}
