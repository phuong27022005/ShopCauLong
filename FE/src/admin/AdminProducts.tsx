import { useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'
import type { ProductDetail } from '../catalog/types'
import { formatVND } from '../catalog/formatVND'
import { Pagination } from '../catalog/components/Pagination'
import { CatalogImage } from '../catalog/components/CatalogImage'
import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'
import { ProductForm } from './ProductForm'

export function AdminProducts() {
  const { api } = useAuth()
  const [search, setSearch] = useState('')
  const [page, setPage] = useState(1)
  const [editing, setEditing] = useState<ProductDetail | null | undefined>(undefined)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  const result = useApiResource<{ items: ProductDetail[]; totalPages: number }>(`/admin/products?page=${page}&pageSize=12&search=${encodeURIComponent(search)}`)
  async function toggle(product: ProductDetail) {
    if (busy) return
    setBusy(true); setError('')
    try {
      const response = await api.put<ApiResponse<ProductDetail>>('/products/' + product.id, { ...product, isActive: !product.isActive })
      if (!response.data.success) throw new Error(response.data.message)
      result.reload()
    } catch (cause) { setError(authErrorMessage(cause, 'Không đổi được trạng thái sản phẩm.')) }
    finally { setBusy(false) }
  }
  return <>
    <h2 className="text-2xl font-bold">Quản lý sản phẩm</h2>
    <div className="flex flex-wrap gap-3"><label>Tìm sản phẩm<input className="field" value={search} maxLength={200} onChange={e => { setSearch(e.target.value); setPage(1) }} /></label>
      <button onClick={() => setEditing(null)} className="rounded border px-4 py-2">Thêm sản phẩm</button></div>
    {editing !== undefined && <ProductForm key={editing?.id ?? 'new'} product={editing ?? undefined} close={() => setEditing(undefined)} saved={() => { setEditing(undefined); result.reload() }} />}
    {error && <p role="alert">{error}</p>}<ResourceState {...result} />
    {result.data && <>
      {!result.data.items.length ? <p>Chưa có sản phẩm.</p> : <div className="overflow-x-auto"><table className="w-full"><thead><tr><th>Sản phẩm</th><th>Giá</th><th>Tồn kho</th><th>Hiển thị</th><th>Thao tác</th></tr></thead><tbody>
        {result.data.items.map(product => <tr key={product.id} className="border-b"><td className="p-2"><CatalogImage src={product.imageUrl} alt="" className="size-12 object-contain" />{product.name}</td>
          <td>{formatVND(product.discountPrice ?? product.price)}</td><td>{product.stock}</td><td>{product.isActive ? 'Đang bán' : 'Đã ẩn'}</td>
          <td><button onClick={() => setEditing(product)} className="p-2 underline">Sửa {product.name}</button><button disabled={busy} onClick={() => void toggle(product)} className="p-2 underline">{product.isActive ? 'Ẩn' : 'Hiện'} {product.name}</button></td></tr>)}
      </tbody></table></div>}
      <Pagination page={page} totalPages={result.data.totalPages} onChange={setPage} />
    </>}
  </>
}

