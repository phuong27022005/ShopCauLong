import { Link } from 'react-router-dom'
import { getBrands, getProducts } from '../catalog/api'
import { CatalogImage } from '../catalog/components/CatalogImage'
import { EmptyState } from '../catalog/components/EmptyState'
import { ErrorState } from '../catalog/components/ErrorState'
import { LoadingSkeleton } from '../catalog/components/LoadingSkeleton'
import { ProductGrid } from '../catalog/components/ProductGrid'
import { useCatalogResource } from '../catalog/useCatalogResource'
import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'

const loadNewest = (signal: AbortSignal) => getProducts(1, 4, signal, true)

export function HomePage() {
  const products = useCatalogResource(loadNewest)
  const brands = useCatalogResource(getBrands)
  const categories = useApiResource<{ id: number; name: string; imageUrl: string | null; productCount: number }[]>('/discovery/categories')

  return (
    <div className="space-y-12">
      <section className="relative isolate overflow-hidden rounded-2xl bg-teal-950 px-6 py-12 text-white sm:px-12 sm:py-16 md:pr-[45%]">
        <img src="/illustrations/court.svg" alt="Minh họa vợt và quả cầu trên sân cầu lông" className="absolute right-0 top-0 -z-10 h-full w-full object-cover opacity-20 md:w-[47%] md:opacity-100" />
        <div aria-hidden="true" className="absolute -right-12 top-3 -z-10 h-56 w-80 rotate-12 rounded border-2 border-teal-700/70 sm:right-10">
          <div className="absolute inset-x-12 inset-y-0 border-x-2 border-teal-700/70" />
          <div className="absolute inset-x-0 top-1/2 border-t-2 border-teal-700/70" />
        </div>
        <p className="text-sm font-semibold uppercase tracking-widest text-teal-200">Badminton Shop</p>
        <h1 className="mt-3 max-w-xl text-3xl font-bold sm:text-5xl">Cửa hàng vợt cầu lông</h1>
        <p className="mt-4 max-w-md text-teal-100">Tìm cây vợt phù hợp với lối chơi và ngân sách của bạn.</p>
        <Link to="/products" className="mt-6 inline-block rounded-lg bg-white px-4 py-2.5 font-semibold text-teal-950">
          Khám phá sản phẩm
        </Link>
        <Link to="/compare" className="ml-4 mt-4 inline-block font-medium text-teal-100 underline">So sánh vợt</Link>
      </section>

      <section className="space-y-4" aria-labelledby="shop-categories"><h2 id="shop-categories" className="text-2xl font-bold">Chọn theo lối chơi</h2>
        <ResourceState {...categories} />
        {categories.data && <div className="grid gap-4 sm:grid-cols-3">{categories.data.map(category => <Link key={category.id} to={'/products?categoryId=' + category.id} className="group flex items-center gap-3 rounded-xl border bg-white p-4 hover:border-teal-600">
          <CatalogImage src={category.imageUrl} alt="" className="h-24 w-20 object-contain transition-transform group-hover:scale-105" /><div><h3 className="font-semibold text-teal-950">{category.name}</h3><p className="mt-1 text-sm text-slate-600">{category.productCount} sản phẩm còn hàng</p></div>
        </Link>)}</div>}
      </section>

      <section aria-labelledby="new-products" className="space-y-4">
        <h2 id="new-products" className="text-2xl font-bold">Sản phẩm mới</h2>
        {products.state.status === 'loading' && <LoadingSkeleton message="Đang tải sản phẩm mới…" />}
        {products.state.status === 'error' && <ErrorState message="Không tải được sản phẩm mới." onRetry={products.retry} />}
        {products.state.status === 'success' && (products.state.data.items.length
          ? <ProductGrid products={products.state.data.items} />
          : <EmptyState message="Chưa có sản phẩm mới." />)}
      </section>

      <section aria-labelledby="brands" className="space-y-4">
        <h2 id="brands" className="text-2xl font-bold">Thương hiệu</h2>
        {brands.state.status === 'loading' && <LoadingSkeleton message="Đang tải thương hiệu…" count={1} />}
        {brands.state.status === 'error' && <ErrorState message="Không tải được thương hiệu." onRetry={brands.retry} />}
        {brands.state.status === 'success' && (brands.state.data.length ? (
          <ul className="flex flex-wrap gap-3">
            {brands.state.data.map((brand) => (
              <li key={brand.id} className="flex items-center gap-2 rounded-lg border border-slate-200 bg-white px-4 py-3 font-medium">
                {brand.logo && <CatalogImage src={brand.logo} alt="" className="size-8 rounded object-contain" />}
                {brand.name}
              </li>
            ))}
          </ul>
        ) : <EmptyState message="Chưa có thương hiệu." />)}
      </section>
    </div>
  )
}
