import { useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'
import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'
import { Pagination } from '../catalog/components/Pagination'

export interface Review { id: number; userId: number; productId: number; rating: number; comment: string; createdAt: string }
export interface ReviewPage { items: Review[]; page: number; totalPages: number; totalItems: number; averageRating: number }
export function Stars({ rating }: { rating: number }) {
  return <span aria-label={rating.toFixed(1) + ' trên 5 sao'} className="text-amber-600"><span aria-hidden="true">{'★'.repeat(Math.round(rating))}{'☆'.repeat(5 - Math.round(rating))}</span> {rating.toFixed(1)}/5</span>
}
export function Reviews({ productId }: { productId: number }) {
  const { user, api } = useAuth()
  const [page, setPage] = useState(1)
  const list = useApiResource<ReviewPage>(`/products/${productId}/reviews?page=${page}&pageSize=5`)
  const eligibility = useApiResource<{ canReview: boolean; reason: string }>(user ? `/products/${productId}/reviews/eligibility` : null)
  const [rating, setRating] = useState(0)
  const [comment, setComment] = useState('')
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  async function submit(e: React.FormEvent) {
    e.preventDefault()
    if (!rating) { setError('Vui lòng chọn số sao.'); return }
    if (busy) return
    setBusy(true); setError('')
    try {
      const response = await api.post<ApiResponse<Review>>(`/products/${productId}/reviews`, { rating, comment })
      if (!response.data.success) throw new Error(response.data.message)
      setRating(0); setComment(''); setPage(1); list.reload(); eligibility.reload()
    } catch (cause) { setError(authErrorMessage(cause, 'Không gửi được đánh giá.')) }
    finally { setBusy(false) }
  }
  return <section className="space-y-4 rounded-xl border bg-white p-5">
    <h2 className="text-xl font-bold">Đánh giá sản phẩm</h2>
    <ResourceState {...list} />
    {list.data && <>
      <Stars rating={list.data.averageRating} /><span> ({list.data.totalItems} đánh giá)</span>
      {!list.data.items.length && <p>Chưa có đánh giá.</p>}
      <ul className="divide-y">{list.data.items.map(review => <li key={review.id} className="py-3">
        <Stars rating={review.rating} /><p className="whitespace-pre-wrap break-words">{review.comment}</p>
      </li>)}</ul>
      <Pagination page={page} totalPages={list.data.totalPages} onChange={setPage} label="Phân trang đánh giá" />
    </>}
    {!user ? <p>Đăng nhập và hoàn tất đơn mua sản phẩm để đánh giá.</p> : <>
      <ResourceState {...eligibility} />
      {eligibility.data?.canReview ? <form onSubmit={submit} className="space-y-3">
        <fieldset><legend>Số sao (bắt buộc)</legend>{[1, 2, 3, 4, 5].map(star => <label key={star} className="mr-3">
          <input type="radio" name="rating" value={star} checked={rating === star} onChange={() => setRating(star)} /> {star} sao
        </label>)}</fieldset>
        <label className="block">Nhận xét<textarea value={comment} maxLength={2000} onChange={e => setComment(e.target.value)} className="block w-full rounded border p-2" /></label>
        <button disabled={busy} className="rounded bg-teal-800 px-4 py-2 text-white">{busy ? 'Đang gửi…' : 'Gửi đánh giá'}</button>
      </form> : eligibility.data && <p>{eligibility.data.reason}</p>}
    </>}
    {error && <p role="alert">{error}</p>}
  </section>
}

