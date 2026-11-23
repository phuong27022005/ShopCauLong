import { createContext, useContext, useEffect, useState } from 'react'
import type { ReactNode } from 'react'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'
import type { ProductDetail, ProductSummary } from '../catalog/types'
import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'
import { ProductGrid } from '../catalog/components/ProductGrid'
import { CatalogImage } from '../catalog/components/CatalogImage'
import { formatVND } from '../catalog/formatVND'
import { AddToCartButton } from '../catalog/LocalCart'
import { EmptyState } from '../catalog/components/EmptyState'

const compareKey = 'badminton-shop.compare'
export function readComparison(): number[] {
  try {
    const stored: unknown = JSON.parse(localStorage.getItem(compareKey) || '[]')
    return Array.isArray(stored) ? [...new Set(stored.filter((x): x is number => Number.isSafeInteger(x) && x > 0))].slice(0, 3) : []
  } catch { return [] }
}
type Tools = { ids: number[]; compare: (id: number) => void; favorites: ProductSummary[]; favorite: (id: number) => Promise<void>; busy: boolean; loading: boolean; error: string; reload: () => void }
const Context = createContext<Tools | null>(null)

export function ShoppingProvider({ children }: { children: ReactNode }) {
  const { user } = useAuth()
  return <ShoppingSession key={user?.id ?? 'guest'}>{children}</ShoppingSession>
}
function ShoppingSession({ children }: { children: ReactNode }) {
  const { api, user } = useAuth()
  const navigate = useNavigate()
  const result = useApiResource<ProductSummary[]>(user ? '/favorites' : null)
  const [ids, setIds] = useState(readComparison)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => { try { localStorage.setItem(compareKey, JSON.stringify(ids)) } catch { /* Selection remains usable in memory. */ } }, [ids])
  const favorites = result.data ?? []
  return <Context.Provider value={{ ids, favorites, busy, loading: result.loading, error: error || result.error, reload: result.reload,
    compare: id => {
      if (!ids.includes(id) && ids.length >= 3) { setError('Chỉ so sánh tối đa 3 sản phẩm. Hãy bỏ một sản phẩm trước.'); return }
      setError(''); setIds(current => current.includes(id) ? current.filter(x => x !== id) : [...current, id].slice(0, 3))
    },
    favorite: async id => {
      if (!user) { void navigate('/login', { state: { returnTo: '/favorites' } }); return }
      if (busy || result.loading) return
      setBusy(true); setError('')
      try {
        await api.request({ method: favorites.some(x => x.id === id) ? 'DELETE' : 'PUT', url: '/favorites/' + id })
        result.reload()
      } catch (cause) { setError(authErrorMessage(cause, 'Không cập nhật được yêu thích.')) }
      finally { setBusy(false) }
    },
  }}>{children}</Context.Provider>
}

export function ProductTools({ id }: { id: number }) {
  const tools = useContext(Context)
  if (!tools) return null
  return <div className="mt-3 flex flex-wrap gap-2 text-sm">
    <button type="button" aria-pressed={tools.favorites.some(x => x.id === id)} disabled={tools.busy || tools.loading}
      onClick={() => void tools.favorite(id)} className="rounded-lg border px-3 py-2 aria-pressed:bg-rose-50 aria-pressed:text-rose-800">{tools.favorites.some(x => x.id === id) ? '♥ Đã yêu thích' : '♡ Yêu thích'}</button>
    <button type="button" aria-pressed={tools.ids.includes(id)} onClick={() => tools.compare(id)} className="rounded-lg border px-3 py-2 aria-pressed:bg-teal-50">{tools.ids.includes(id) ? 'Bỏ so sánh' : 'So sánh'}</button>
  </div>
}

export function ShoppingNotice() {
  const tools = useContext(Context)
  if (!tools) return null
  return <>
    {tools.error && <div role="alert" className="mb-4 rounded-lg bg-amber-50 p-3 text-amber-950">{tools.error} <button onClick={tools.reload} className="underline">Thử lại</button></div>}
    {tools.ids.length > 0 && <div className="mb-5 flex flex-wrap items-center justify-between gap-2 rounded-xl border border-teal-200 bg-teal-50 p-3">
      <span>Đã chọn {tools.ids.length}/3 sản phẩm</span><Link to="/compare" className="font-semibold text-teal-900 underline">Mở bảng so sánh</Link>
    </div>}
  </>
}

export function FavoritesPage() {
  const tools = useContext(Context)!
  return <section className="space-y-5"><h1 className="text-3xl font-bold">Sản phẩm yêu thích</h1>
    {tools.loading ? <p role="status">Đang tải yêu thích…</p> : tools.favorites.length ? <ProductGrid products={tools.favorites} /> : <EmptyState message="Bạn chưa lưu sản phẩm nào. Nhấn Yêu thích trên sản phẩm để lưu tại đây." />}
    <Link to="/products" className="inline-block underline">Khám phá sản phẩm</Link></section>
}

export function ComparePage() {
  const tools = useContext(Context)!
  const result = useApiResource<ProductDetail[]>(tools.ids.length ? '/products/compare?' + tools.ids.map(id => 'ids=' + id).join('&') : null)
  const rows: [string, (p: ProductDetail) => string][] = [
    ['Giá bán', p => formatVND(p.discountPrice ?? p.price)], ['Thương hiệu', p => p.brand.name],
    ['Trọng lượng', p => p.weight], ['Độ cứng', p => p.stiffness], ['Điểm cân bằng', p => p.balancePoint],
    ['Trình độ phù hợp', p => p.skillLevel || 'Chưa cập nhật'], ['Tồn kho', p => p.stock > 0 ? `${p.stock} sản phẩm` : 'Hết hàng'],
  ]
  return <section className="space-y-5"><h1 className="text-3xl font-bold">So sánh vợt</h1><p className="text-slate-600">Đặt tối đa 3 mẫu cạnh nhau để chọn cây vợt phù hợp với bạn.</p>
    {!tools.ids.length ? <EmptyState message="Chọn So sánh ở sản phẩm bạn muốn xem." /> : <>
      <ResourceState {...result} />
      {result.data && <>
        {tools.ids.filter(id => !result.data!.some(p => p.id === id)).map(id => <p key={id}>Sản phẩm #{id} không còn bán. <button className="underline" onClick={() => tools.compare(id)}>Bỏ khỏi so sánh</button></p>)}
        {result.data.length > 0 && <div className="overflow-x-auto rounded-xl border bg-white"><table className="w-full min-w-[560px] text-left"><caption className="sr-only">Thông số các sản phẩm đã chọn</caption>
          <thead><tr><th className="p-4">Thông số</th>{result.data.map(p => <th key={p.id} className="min-w-44 p-4"><CatalogImage src={p.imageUrl} alt="" className="mx-auto h-40 w-full object-contain" /><Link className="block py-3 text-teal-900 underline" to={'/products/' + p.slug}>{p.name}</Link><button className="text-sm font-normal underline" onClick={() => tools.compare(p.id)}>Bỏ {p.name}</button></th>)}</tr></thead>
          <tbody>{rows.map(([label, value]) => <tr key={label} className="border-t even:bg-slate-50"><th scope="row" className="p-4 text-sm">{label}</th>{result.data!.map(p => <td key={p.id} className="p-4">{value(p) || 'Chưa cập nhật'}</td>)}</tr>)}
          <tr className="border-t"><th className="p-4">Mua hàng</th>{result.data.map(p => <td key={p.id} className="p-4"><AddToCartButton product={p} /></td>)}</tr></tbody>
        </table></div>}
      </>}
    </>}
    <Link to="/products" className="inline-block underline">Chọn thêm sản phẩm</Link>
  </section>
}
