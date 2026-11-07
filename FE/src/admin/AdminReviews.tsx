import { useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'
import { Stars, type Review } from '../reviews/Reviews'
import { Pagination } from '../catalog/components/Pagination'
import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'

export function AdminReviews() {
  const { api } = useAuth()
  const [page, setPage] = useState(1)
  const result = useApiResource<{ items: Review[]; totalPages: number }>(`/admin/reviews?page=${page}&pageSize=12`)
  const [selected, setSelected] = useState<Review | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function remove() {
    if (!selected || busy) return
    setBusy(true); setError('')
    try {
      const response = await api.delete<ApiResponse<null>>(`/products/${selected.productId}/reviews/${selected.id}`)
      if (!response.data.success) throw new Error(response.data.message)
      setSelected(null); result.reload()
    } catch (cause) { setError(authErrorMessage(cause, 'Không xóa được đánh giá.')) }
    finally { setBusy(false) }
  }
  return <>
    <h2 className="text-2xl font-bold">Quản lý đánh giá</h2><ResourceState {...result} />
    {error && <p role="alert">{error}</p>}
    {result.data && <>
      {!result.data.items.length && <p>Chưa có đánh giá.</p>}
      <ul className="space-y-3">{result.data.items.map(review => <li key={review.id} className="rounded border bg-white p-4">
        <p>Sản phẩm #{review.productId} · Khách #{review.userId}</p><Stars rating={review.rating} /><p className="break-words">{review.comment}</p>
        <button onClick={() => setSelected(review)} className="underline">Xóa đánh giá #{review.id}</button>
      </li>)}</ul>
      <Pagination page={page} totalPages={result.data.totalPages} onChange={setPage} label="Phân trang kiểm duyệt" />
    </>}
    {selected && <div role="dialog" aria-modal="true" aria-label="Xác nhận xóa đánh giá" className="rounded border bg-white p-4">
      <p>Xóa đánh giá #{selected.id}?</p><button disabled={busy} onClick={() => void remove()} className="mr-4 underline">Xác nhận xóa</button>
      <button disabled={busy} onClick={() => setSelected(null)}>Giữ đánh giá</button>
    </div>}
  </>
}

