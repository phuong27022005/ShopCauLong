import { useCallback, useState } from 'react'
import { useSearchParams } from 'react-router-dom'
import { getBrands, getFilteredProducts } from '../catalog/api'
import { EmptyState } from '../catalog/components/EmptyState'
import { ErrorState } from '../catalog/components/ErrorState'
import { LoadingSkeleton } from '../catalog/components/LoadingSkeleton'
import { Pagination } from '../catalog/components/Pagination'
import { ProductGrid } from '../catalog/components/ProductGrid'
import { FilterDrawer } from '../catalog/components/FilterDrawer'
import { FilterTags } from '../catalog/components/FilterTags'
import { ProductFilters } from '../catalog/components/ProductFilters'
import { SearchInput } from '../catalog/components/SearchInput'
import { useCatalogResource } from '../catalog/useCatalogResource'

export function ProductsPage() {
  const [params, setParams] = useSearchParams()
  const [drawer, setDrawer] = useState(false)
  const [resetCount, setResetCount] = useState(0)
  const query = params.toString()
  const requestedPage = Number(params.get('page') ?? 1)
  const page = Number.isInteger(requestedPage) && requestedPage >= 1 && requestedPage <= 2_147_483_647
    ? requestedPage : 1
  const load = useCallback((signal: AbortSignal) => {
    const request = new URLSearchParams(query)
    request.set('page', String(page))
    if (!request.has('pageSize')) request.set('pageSize', '12')
    return getFilteredProducts(request, signal)
  }, [query, page])
  const { state, retry } = useCatalogResource(load)
  const brandResource = useCatalogResource(getBrands)
  const brands = brandResource.state.status === 'success' ? brandResource.state.data : []

  const changeFilter = useCallback((key: string, values: string[]) => {
    setParams((current) => {
      const next = new URLSearchParams(current)
      next.delete('page')
      if (key === 'priceRange') {
        next.delete('minPrice')
        next.delete('maxPrice')
        if (values[0]) next.set('minPrice', values[0])
        if (values[1]) next.set('maxPrice', values[1])
      } else if (key === 'sort') {
        next.delete('sortBy')
        next.delete('sortDir')
        next.delete('newestFirst')
        if (values[0]) {
          next.set('sortBy', values[0])
          next.set('sortDir', values[1])
        }
      } else {
        next.delete(key)
        values.filter(Boolean).forEach((value) => next.append(key, value))
      }
      return next
    })
  }, [setParams])
  const search = useCallback((value: string) => changeFilter('search', [value]), [changeFilter])
  function reset() {
    setResetCount((count) => count + 1)
    setParams({})
  }
  const filters = <>
    {brandResource.state.status === 'loading' && <p>Đang tải thương hiệu…</p>}
    {brandResource.state.status === 'error' && <ErrorState message="Không tải được thương hiệu." onRetry={brandResource.retry} />}
    <ProductFilters key={query + resetCount} params={params} brands={brands} onChange={changeFilter} />
  </>

  return (
    <section className="space-y-6">
      <h1 className="text-3xl font-bold">Sản phẩm</h1>
      <div className="flex flex-wrap items-end gap-4">
        <SearchInput key={query + resetCount} value={params.get('search') ?? ''} onChange={search} />
        <label>Sắp xếp
          <select className="mt-1 block rounded-lg border bg-white p-2"
            value={params.has('sortBy') ? params.get('sortBy') + ':' + (params.get('sortDir') ?? 'asc') : ''}
            onChange={(event) => changeFilter('sort', event.target.value.split(':'))}>
            <option value="">Mặc định</option>
            <option value="price:asc">Giá tăng dần</option><option value="price:desc">Giá giảm dần</option>
            <option value="name:asc">Tên A–Z</option><option value="name:desc">Tên Z–A</option>
            <option value="createdAt:desc">Mới nhất</option><option value="createdAt:asc">Cũ nhất</option>
          </select>
        </label>
        <button type="button" onClick={() => setDrawer(true)} className="rounded border px-3 py-2 md:hidden">Mở bộ lọc</button>
        <button type="button" onClick={reset} className="rounded border px-3 py-2">Xóa bộ lọc</button>
      </div>
      <FilterTags params={params} brands={brands} onChange={changeFilter} />
      <div className="grid gap-6 md:grid-cols-[210px_minmax(0,1fr)]">
        <aside aria-label="Bộ lọc sản phẩm" className="hidden md:block">{!drawer && filters}</aside>
        {drawer && <FilterDrawer onClose={() => setDrawer(false)}>{filters}</FilterDrawer>}
        <div className="min-w-0 space-y-6">
          {state.status === 'loading' && <LoadingSkeleton message="Đang tải sản phẩm…" />}
          {state.status === 'error' && <ErrorState message="Không tải được danh sách sản phẩm." onRetry={retry} />}
          {state.status === 'success' && <>
            {state.data.items.length ? <ProductGrid products={state.data.items} /> : <div className="space-y-3">
              <EmptyState message="Không có sản phẩm trên trang này." />
              <button type="button" onClick={reset} className="text-teal-800 underline">Xóa bộ lọc để xem thêm sản phẩm</button>
            </div>}
            {state.data.totalItems > 0 && <Pagination page={page}
              totalPages={Math.ceil(state.data.totalItems / state.data.pageSize)}
              onChange={(nextPage) => setParams((current) => {
                const next = new URLSearchParams(current)
                if (nextPage === 1) next.delete('page')
                else next.set('page', String(nextPage))
                return next
              })} />}
          </>}
        </div>
      </div>
    </section>
  )
}
