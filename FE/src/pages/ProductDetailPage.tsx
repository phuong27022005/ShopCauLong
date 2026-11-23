import { Reviews } from '../reviews/Reviews'
import { ProductTools } from '../shopping/ShoppingTools'
import { useCallback, useRef, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { getProduct, isProductNotFound } from '../catalog/api'
import { AddToCartButton } from '../catalog/LocalCart'
import { CatalogImage } from '../catalog/components/CatalogImage'
import { ErrorState } from '../catalog/components/ErrorState'
import { LoadingSkeleton } from '../catalog/components/LoadingSkeleton'
import { PriceTag } from '../catalog/components/PriceTag'
import { useCatalogResource } from '../catalog/useCatalogResource'

function ProductGallery({ name, cover, additional }: { name: string; cover: string | null; additional: string[] }) {
  const [selected, setSelected] = useState(0)
  const dialog = useRef<HTMLDialogElement>(null)
  const images = [cover, ...additional]

  return (
    <div className="space-y-3">
      <button type="button" className="block w-full cursor-zoom-in rounded-2xl" aria-label="Phóng to ảnh sản phẩm" onClick={() => dialog.current?.showModal()}><CatalogImage
        src={images[selected] ?? cover}
        alt={selected === 0 ? name : `${name} – ảnh ${selected + 1}`}
        className="aspect-square w-full rounded-2xl border border-slate-200 bg-white object-contain"
      /></button>
      <dialog ref={dialog} className="m-auto max-h-[90vh] w-[min(90vw,800px)] rounded-2xl p-5 backdrop:bg-black/70" onClick={event => { if (event.target === event.currentTarget) dialog.current?.close() }}>
        <button type="button" autoFocus onClick={() => dialog.current?.close()} className="mb-3 rounded border px-4 py-2">Đóng ảnh</button>
        <CatalogImage src={images[selected] ?? cover} alt={name} className="max-h-[70vh] w-full object-contain" />
      </dialog>
      {additional.length > 0 && (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Ảnh sản phẩm">
          {images.map((src, index) => (
            <button
              key={index}
              type="button"
              aria-label={`Xem ảnh ${index + 1} của ${name}`}
              aria-pressed={selected === index}
              onClick={() => setSelected(index)}
              className="rounded-lg border-2 border-slate-200 bg-white p-1 aria-pressed:border-teal-700"
            >
              <CatalogImage src={src} alt="" className="size-16 rounded object-contain" />
            </button>
          ))}
        </div>
      )}
    </div>
  )
}

export function ProductDetailPage() {
  const { slug = '' } = useParams()
  const load = useCallback((signal: AbortSignal) => getProduct(slug, signal), [slug])
  const { state, retry } = useCatalogResource(load)

  if (state.status === 'loading') return <LoadingSkeleton message="Đang tải chi tiết sản phẩm…" count={1} />
  if (state.status === 'error') {
    if (isProductNotFound(state.error)) {
      return (
        <section className="space-y-3">
          <p className="font-semibold text-teal-800">404</p>
          <h1 className="text-2xl font-bold">Không tìm thấy sản phẩm</h1>
          <Link to="/products" className="inline-block underline">Về danh sách sản phẩm</Link>
        </section>
      )
    }
    return <ErrorState message="Không tải được chi tiết sản phẩm." onRetry={retry} />
  }

  const product = state.data
  const specs = [
    ['Trọng lượng', product.weight],
    ['Độ cứng', product.stiffness],
    ['Điểm cân bằng', product.balancePoint],
    ['Trình độ phù hợp', product.skillLevel],
  ]

  return (
    <div className="space-y-5">
      <Link to="/products" className="text-sm font-medium text-teal-800 underline">Về danh sách sản phẩm</Link>
      <article className="grid gap-6 md:grid-cols-2">
        <ProductGallery key={product.id} name={product.name} cover={product.imageUrl}
          additional={product.additionalImageUrls ?? []} />
        <div className="space-y-4">
          <p className="text-sm text-slate-600">{product.brand.name} · {product.category.name}</p>
          <h1 className="break-words text-3xl font-bold">{product.name}</h1>
          <PriceTag price={product.price} discountPrice={product.discountPrice} />
          <p className="font-medium">{product.stock > 0 ? `Còn ${product.stock} sản phẩm` : 'Tồn kho: 0'}</p>
          <AddToCartButton product={product} chooseQuantity />
          <ProductTools id={product.id} />
          <section className="space-y-2" aria-labelledby="specs">
            <h2 id="specs" className="text-lg font-bold">Thông số</h2>
            <dl className="divide-y divide-slate-200 rounded-lg border border-slate-200 bg-white px-4">
              {specs.map(([label, value]) => (
                <div key={label} className="flex justify-between gap-4 py-2 text-sm">
                  <dt className="text-slate-600">{label}</dt>
                  <dd className="text-right font-medium">{value || 'Chưa cập nhật'}</dd>
                </div>
              ))}
            </dl>
          </section>
          <section className="space-y-2" aria-labelledby="description">
            <h2 id="description" className="text-lg font-bold">Mô tả</h2>
            <p className="whitespace-pre-line break-words text-slate-700">{product.description || 'Chưa có mô tả.'}</p>
          </section>
        </div>
      </article>
      <Reviews key={product.id} productId={product.id} />
    </div>
  )
}
