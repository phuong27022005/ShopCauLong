import { useState } from 'react'
import { Link } from 'react-router-dom'
import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'
import { Pagination } from '../catalog/components/Pagination'

type Inventory = { items: { id: number; name: string; stock: number; reserved: number; isActive: boolean }[]; totalPages: number }
export function AdminInventory() {
  const [page, setPage] = useState(1)
  const [search, setSearch] = useState('')
  const [draft, setDraft] = useState('')
  const result = useApiResource<Inventory>(`/admin/inventory?page=${page}&pageSize=12&search=${encodeURIComponent(search)}`)
  return <section className="space-y-4"><h2 className="text-2xl font-bold">Tồn kho và hàng đang giữ</h2>
    <p className="text-slate-600">Khả dụng là số lượng còn có thể đặt. Hàng đang giữ thuộc đơn chờ xác nhận hoặc đã xác nhận; đã được trừ khỏi số khả dụng.</p>
    <form className="flex gap-2" onSubmit={event => { event.preventDefault(); setPage(1); setSearch(draft) }}><label className="grow">Tìm sản phẩm<input className="field" maxLength={200} value={draft} onChange={e => setDraft(e.target.value)} /></label><button className="self-end rounded-lg border p-2">Tìm</button></form>
    <ResourceState {...result} />
    {result.data && <><div className="overflow-auto rounded-xl border bg-white"><table className="w-full text-left"><thead><tr><th className="p-3">Sản phẩm</th><th className="p-3">Khả dụng</th><th className="p-3">Đang giữ</th><th className="p-3">Tình trạng</th></tr></thead><tbody>
      {result.data.items.map(item => <tr className="border-t" key={item.id}><td className="p-3">{item.name}</td><td className="p-3 font-semibold">{item.stock}</td><td className="p-3">{item.reserved}</td><td className="p-3">{!item.isActive ? 'Đã ẩn' : item.stock === 0 ? 'Hết hàng' : item.stock <= 5 ? 'Sắp hết hàng' : 'Còn hàng'}</td></tr>)}
    </tbody></table>{!result.data.items.length && <p className="p-4">Không tìm thấy sản phẩm.</p>}</div><Pagination label="Phân trang tồn kho" page={page} totalPages={result.data.totalPages} onChange={setPage} /></>}
    <button onClick={result.reload} className="mr-4 underline">Làm mới tồn kho</button><Link to="/admin/products" className="underline">Quản lý sản phẩm</Link>
  </section>
}
