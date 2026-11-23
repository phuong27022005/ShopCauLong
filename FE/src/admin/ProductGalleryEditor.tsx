import { useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'
import { CatalogImage } from '../catalog/components/CatalogImage'
import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'

type Gallery = { imageUrl: string | null; additionalImageUrls: string[] }
export function ProductGalleryEditor({ id }: { id: number }) {
  const { api } = useAuth()
  const result = useApiResource<Gallery>(`/products/${id}/gallery`)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const urls = result.data ? [result.data.imageUrl, ...result.data.additionalImageUrls].filter((x): x is string => Boolean(x)) : []
  async function change(url: string, cover: boolean) {
    if (busy) return
    setBusy(true); setError('')
    try {
      await api.request({ method: cover ? 'PUT' : 'DELETE', url: `/products/${id}/gallery${cover ? '/cover' : ''}`, data: { url } })
      result.reload()
    } catch (cause) { setError(authErrorMessage(cause, 'Không cập nhật được ảnh.')) }
    finally { setBusy(false) }
  }
  async function upload(files: File[]) {
    if (!files.length || busy) return
    if (urls.length + files.length > 8) { setError('Mỗi sản phẩm tối đa 8 ảnh.'); return }
    if (files.some(f => !/\.(jpe?g|png|webp)$/i.test(f.name) || !['image/jpeg', 'image/png', 'image/webp'].includes(f.type) || !f.size || f.size > 2 * 1024 * 1024)) {
      setError('Chỉ chọn JPG, PNG, WebP; mỗi ảnh lớn hơn 0 và không quá 2 MB.'); return
    }
    setBusy(true); setError('')
    let uploaded = 0
    try {
      for (const file of files) {
        const data = new FormData(); data.append('file', file)
        await api.post<ApiResponse<Gallery>>(`/products/${id}/gallery`, data)
        uploaded++
      }
    } catch (cause) { setError(`Đã tải ${uploaded}/${files.length} ảnh. ` + authErrorMessage(cause, 'Không tải được ảnh tiếp theo.')) }
    finally { setBusy(false); result.reload() }
  }
  return <section className="space-y-3 rounded-lg border bg-slate-50 p-4">
    <h4 className="font-semibold">Thư viện ảnh ({urls.length}/8)</h4><p className="text-sm text-slate-600">Ảnh thư viện được lưu ngay. Chọn ảnh đại diện hoặc bỏ ảnh khỏi sản phẩm.</p>
    <ResourceState {...result} />
    <div className="flex flex-wrap gap-3">{urls.map(url => <div key={url} className="w-32 rounded-lg border bg-white p-2"><CatalogImage src={url} alt="Ảnh thư viện" className="h-28 w-full object-contain" />
      {url === result.data?.imageUrl ? <p className="py-2 text-sm font-semibold text-teal-800">Ảnh đại diện</p> : <button type="button" disabled={busy} className="py-2 text-sm underline" onClick={() => void change(url, true)}>Đặt đại diện</button>}
      <button type="button" disabled={busy} className="block text-sm text-red-800 underline" onClick={() => void change(url, false)}>Bỏ ảnh</button>
    </div>)}</div>
    <label className="block">Thêm nhiều ảnh<input disabled={busy || result.loading || urls.length >= 8} type="file" multiple accept=".jpg,.jpeg,.png,.webp" className="field" onChange={event => { const files = Array.from(event.target.files ?? []); event.target.value = ''; void upload(files) }} /></label>
    {busy && <p role="status">Đang cập nhật thư viện…</p>}{error && <p role="alert" className="text-red-800">{error}</p>}
  </section>
}
