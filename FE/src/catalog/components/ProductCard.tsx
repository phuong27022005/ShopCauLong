import { Link } from 'react-router-dom'
import { AddToCartButton } from '../LocalCart'
import type { ProductSummary } from '../types'
import { CatalogImage } from './CatalogImage'
import { PriceTag } from './PriceTag'

export function ProductCard({ product }: { product: ProductSummary }) {
  return (
    <article className="flex h-full flex-col overflow-hidden rounded-xl border border-slate-200 bg-white">
      <Link to={`/products/${encodeURIComponent(product.slug)}`} className="group block grow focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-teal-800">
        <CatalogImage src={product.imageUrl} alt="" className="aspect-square w-full bg-slate-100 object-contain" />
        <div className="space-y-1 p-4">
          <p className="text-sm text-slate-600">{product.brandName}</p>
          <h3 className="break-words font-semibold group-hover:underline">{product.name}</h3>
          <PriceTag price={product.price} discountPrice={product.discountPrice} />
        </div>
      </Link>
      <div className="px-4 pb-4">
        <AddToCartButton product={{ ...product, isActive: true }} />
      </div>
    </article>
  )
}
