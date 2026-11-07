import type { ProductSummary } from '../types'
import { ProductCard } from './ProductCard'

export function ProductGrid({ products }: { products: ProductSummary[] }) {
  return (
    <ul className="grid grid-cols-1 gap-4 min-[420px]:grid-cols-2 lg:grid-cols-4">
      {products.map((product) => <li key={product.id}><ProductCard product={product} /></li>)}
    </ul>
  )
}
