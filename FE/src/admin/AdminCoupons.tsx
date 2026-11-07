import { useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'
import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'
import { formatVND } from '../catalog/formatVND'

const schema = z.object({
  code: z.string().trim().min(1, 'Nhập mã giảm giá.').max(50).regex(/^[a-zA-Z0-9_-]+$/, 'Mã chỉ gồm chữ, số, _ và -.'),
  type: z.enum(['Percent', 'Fixed']), value: z.number().positive('Giá trị phải lớn hơn 0.'),
  minOrderAmount: z.number().nonnegative(), maxDiscount: z.number().nonnegative().nullable(),
  usageLimit: z.number().int().positive(), startAt: z.string().min(1, 'Chọn ngày bắt đầu.'),
  endAt: z.string().min(1, 'Chọn ngày kết thúc.'), isActive: z.boolean(),
}).refine(x => x.type !== 'Percent' || x.value <= 100, { path: ['value'], message: 'Phần trăm không vượt 100.' })
  .refine(x => x.endAt > x.startAt, { path: ['endAt'], message: 'Ngày kết thúc phải sau ngày bắt đầu.' })
type Input = z.infer<typeof schema>
interface Coupon extends Input { id: number; usedCount: number }
function localDate(value?: string) {
  if (!value) return ''
  const date = new Date(/Z$|[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z')
  return new Date(date.getTime() - date.getTimezoneOffset() * 60000).toISOString().slice(0, 16)
}
function CouponForm({ coupon, saved, close }: { coupon: Coupon | null; saved: () => void; close: () => void }) {
  const { api } = useAuth()
  const [error, setError] = useState('')
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<Input>({
    resolver: zodResolver(schema), defaultValues: coupon ? { ...coupon, startAt: localDate(coupon.startAt), endAt: localDate(coupon.endAt) }
      : { code: '', type: 'Percent', value: 10, minOrderAmount: 0, maxDiscount: null, usageLimit: 100, startAt: '', endAt: '', isActive: true },
  })
  async function submit(input: Input) {
    setError('')
    try {
      const response = await api.request<ApiResponse<Coupon>>({ url: '/admin/coupons' + (coupon ? '/' + coupon.id : ''), method: coupon ? 'PUT' : 'POST',
        data: { ...input, startAt: new Date(input.startAt).toISOString(), endAt: new Date(input.endAt).toISOString() } })
      if (!response.data.success) throw new Error(response.data.message)
      saved()
    } catch (cause) { setError(authErrorMessage(cause, 'Không lưu được mã.')) }
  }
  return <form noValidate onSubmit={handleSubmit(submit)} className="space-y-3 rounded border bg-white p-4">
    <h3 className="font-bold">{coupon ? 'Sửa mã' : 'Thêm mã'}</h3><div className="grid gap-3 sm:grid-cols-2">
      <label>Mã<input {...register('code')} className="field" /></label>
      <label>Loại giảm<select {...register('type')} className="field"><option value="Percent">Phần trăm</option><option value="Fixed">Số tiền</option></select></label>
      <label>Giá trị<input type="number" step="0.01" {...register('value', { valueAsNumber: true })} className="field" /></label>
      <label>Đơn tối thiểu<input type="number" {...register('minOrderAmount', { valueAsNumber: true })} className="field" /></label>
      <label>Giảm tối đa<input type="number" {...register('maxDiscount', { setValueAs: x => x === '' ? null : Number(x) })} className="field" /></label>
      <label>Giới hạn lượt<input type="number" {...register('usageLimit', { valueAsNumber: true })} className="field" /></label>
      <label>Bắt đầu<input type="datetime-local" {...register('startAt')} className="field" /></label>
      <label>Kết thúc<input type="datetime-local" {...register('endAt')} className="field" /></label>
    </div><label className="block"><input type="checkbox" {...register('isActive')} /> Đang hoạt động</label>
    {Object.entries(errors).map(([key, value]) => <p key={key} role="alert">{value.message}</p>)}{error && <p role="alert">{error}</p>}
    <button disabled={isSubmitting} className="rounded bg-teal-800 px-3 py-2 text-white">Lưu mã</button><button type="button" disabled={isSubmitting} onClick={close} className="ml-4 underline">Đóng</button>
  </form>
}
export function AdminCoupons() {
  const { api } = useAuth()
  const result = useApiResource<Coupon[]>('/admin/coupons')
  const [editing, setEditing] = useState<Coupon | null | undefined>(undefined)
  const [deleting, setDeleting] = useState<Coupon | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  async function remove() {
    if (!deleting || busy) return
    setBusy(true); setError('')
    try {
      const response = await api.delete<ApiResponse<null>>('/admin/coupons/' + deleting.id)
      if (!response.data.success) throw new Error(response.data.message)
      setDeleting(null); result.reload()
    } catch (cause) { setError(authErrorMessage(cause, 'Không xóa được mã.')) }
    finally { setBusy(false) }
  }
  return <>
    <h2 className="text-2xl font-bold">Quản lý mã giảm giá</h2><button className="rounded border px-3 py-2" onClick={() => setEditing(null)}>Thêm mã</button>
    {editing !== undefined && <CouponForm key={editing?.id ?? 'new'} coupon={editing} close={() => setEditing(undefined)} saved={() => { setEditing(undefined); result.reload() }} />}
    <ResourceState {...result} />{error && <p role="alert">{error}</p>}
    {result.data && (result.data.length ? <div className="overflow-x-auto"><table className="w-full"><thead><tr><th>Mã</th><th>Giảm</th><th>Lượt dùng</th><th>Hoạt động</th><th>Thao tác</th></tr></thead><tbody>
      {result.data.map(x => <tr key={x.id} className="border-b"><td>{x.code}</td><td>{x.type === 'Percent' ? x.value + '%' : formatVND(x.value)}</td><td>{x.usedCount}/{x.usageLimit}</td>
        <td>{x.isActive ? 'Bật' : 'Tắt'}</td><td><button onClick={() => setEditing(x)} className="p-2 underline">Sửa {x.code}</button><button onClick={() => setDeleting(x)} className="p-2 underline">Xóa {x.code}</button></td></tr>)}
    </tbody></table></div> : <p>Chưa có mã giảm giá.</p>)}
    {deleting && <div role="dialog" aria-modal="true" aria-label="Xác nhận xóa mã" className="rounded border bg-white p-4">
      <p>Xóa {deleting.code}? Mã đã dùng trong đơn sẽ được tắt để giữ lịch sử.</p>
      <button disabled={busy} onClick={() => void remove()} className="mr-4 underline">Xác nhận xóa</button><button disabled={busy} onClick={() => setDeleting(null)}>Giữ mã</button>
    </div>}
  </>
}

