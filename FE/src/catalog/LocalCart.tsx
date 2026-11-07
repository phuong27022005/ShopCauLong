import { useState } from 'react'
import { useCart } from '../cart/CartContext'
import type { CartProduct } from '../cart/CartContext'

export function AddToCartButton({ product, chooseQuantity = false }: { product: CartProduct; chooseQuantity?: boolean }) {
  const cart = useCart()
  const [quantity, setQuantity] = useState(1)
  const existing = cart.items.find(x => x.productId === product.id)?.quantity ?? 0
  const limit = Math.max(0, Math.min(10, product.stock) - existing)
  return <div className="space-y-2">
    {chooseQuantity && <label className="block">Số lượng
      <input type="number" min={1} max={Math.max(1, limit)} value={quantity}
        onChange={event => setQuantity(Number(event.target.value))}
        className="ml-3 w-20 rounded border p-2" />
    </label>}
    <button type="button" disabled={cart.busy || limit === 0}
      onClick={() => void cart.add(product, quantity)}
      className="w-full rounded-lg bg-teal-800 px-4 py-2.5 text-sm font-semibold text-white disabled:bg-slate-200 disabled:text-slate-600">
      {product.stock <= 0 ? 'Hết hàng' : limit === 0 ? 'Đã đạt số lượng tồn kho' : cart.busy ? 'Đang xử lý…' : 'Thêm vào giỏ'}
    </button>
  </div>
}
