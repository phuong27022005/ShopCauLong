import { createContext, useContext, useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { z } from 'zod'
import { useAuth } from '../auth/AuthContext'
import type { ApiResponse } from '../auth/types'
import { authErrorMessage } from '../auth/client'
import { getProduct } from '../catalog/api'

export const CART_KEY = 'badminton-shop.cart'
const productSchema = z.object({
  id: z.number().int().positive(), name: z.string(), slug: z.string(), imageUrl: z.string().nullable(),
  price: z.number().nonnegative(), discountPrice: z.number().nonnegative().nullable(),
  stock: z.number().int().nonnegative(), isActive: z.boolean(),
})
const localSchema = z.array(z.object({ product: productSchema, quantity: z.number().int().min(1).max(10) }))
export type CartProduct = z.infer<typeof productSchema>
export interface CartLine { productId: number; product: CartProduct; quantity: number; unitPrice: number; lineTotal: number; isAvailable: boolean }
interface CartData { items: CartLine[]; totalAmount: number }
function calculate(items: { product: CartProduct; quantity: number }[]): CartData {
  const lines = items.map(({ product, quantity }) => {
    const unitPrice = product.discountPrice ?? product.price
    return { productId: product.id, product, quantity, unitPrice, lineTotal: quantity * unitPrice,
      isAvailable: product.isActive && product.stock >= quantity }
  })
  return { items: lines, totalAmount: lines.reduce((total, line) => total + line.lineTotal, 0) }
}
function readLocal(): CartData {
  try { return calculate(localSchema.parse(JSON.parse(localStorage.getItem(CART_KEY) ?? '[]'))) }
  catch { return calculate([]) }
}
function persist(data: CartData) {
  localStorage.setItem(CART_KEY, JSON.stringify(data.items.map(({ product, quantity }) => ({ product, quantity }))))
}
interface CartValue extends CartData {
  busy: boolean; pendingId: number | null; error: string; notice: string
  add: (product: CartProduct, quantity: number) => Promise<void>
  update: (id: number, quantity: number) => void
  remove: (id: number) => Promise<void>
  retry: () => void
  completeOrder: () => void
}
const CartContext = createContext<CartValue | null>(null)

export function CartProvider({ children }: { children: ReactNode }) {
  const { user, status } = useAuth()
  return <CartSession key={user?.id ?? 'guest'} authenticated={Boolean(user)} checking={status !== 'ready'}>{children}</CartSession>
}
function CartSession({ children, authenticated, checking }: { children: ReactNode; authenticated: boolean; checking: boolean }) {
  const { api } = useAuth()
  const [data, setData] = useState<CartData>(() => authenticated ? calculate([]) : readLocal())
  const current = useRef(data)
  const [busy, setBusy] = useState(true)
  const locked = useRef(true)
  const [pendingId, setPendingId] = useState<number | null>(null)
  const pending = useRef<number | null>(null)
  const timer = useRef<ReturnType<typeof setTimeout> | undefined>(undefined)
  const [error, setError] = useState('')
  const [notice, setNotice] = useState('')
  const [attempt, setAttempt] = useState(0)
  const initialization = useRef<Promise<CartData> | null>(null)
  const alive = useRef(true)
  function lock(value: boolean) { locked.current = value; setBusy(value) }
  function commit(next: CartData) {
    if (!alive.current) return
    if (!authenticated) persist(next)
    current.current = next
    setData(next)
  }
  async function request(path: string, method: string, body?: unknown) {
    const response = await api.request<ApiResponse<CartData>>({ url: path, method, data: body })
    if (!response.data.success) throw new Error(response.data.message)
    return response.data.data
  }
  useEffect(() => {
    alive.current = true
    return () => { alive.current = false; clearTimeout(timer.current) }
  }, [])
  useEffect(() => {
    if (checking) return
    let active = true
    lock(true)
    setError('')
    // Dùng chung promise để StrictMode không gửi merge hai lần.
    initialization.current ??= (async () => {
      if (authenticated) {
        const local = readLocal()
        if (local.items.length) {
          const snapshot = localStorage.getItem(CART_KEY)
          const merged = await request('/cart/merge', 'POST', { items: local.items.map(x => ({ productId: x.productId, quantity: x.quantity })) })
          if (localStorage.getItem(CART_KEY) === snapshot) localStorage.removeItem(CART_KEY)
          return merged
        }
        return request('/cart', 'GET')
      }
      const local = readLocal()
      const refreshed = await Promise.all(local.items.map(async (line) => {
        const product = await getProduct(line.product.slug, new AbortController().signal)
          .catch(() => ({ ...line.product, isActive: false }))
        return { product, quantity: line.quantity }
      }))
      return calculate(refreshed)
    })()
    void initialization.current.then(next => {
      if (active) { commit(next); lock(false) }
    }).catch((cause: unknown) => {
      if (active) setError(authErrorMessage(cause, 'Không tải được giỏ hàng.'))
    })
    return () => { active = false }
  }, [authenticated, checking, attempt])

  async function mutate(operation: () => Promise<CartData>, message: string) {
    if (locked.current || checking) return
    lock(true); setError(''); setNotice('')
    try { commit(await operation()); if (alive.current) setNotice(message) }
    catch (cause) { if (alive.current) setError(authErrorMessage(cause, 'Không cập nhật được giỏ hàng.')) }
    finally { if (alive.current) lock(false) }
  }
  async function add(product: CartProduct, quantity: number) {
    await mutate(async () => {
      const existing = current.current.items.find(x => x.productId === product.id)
      const total = (existing?.quantity ?? 0) + quantity
      if (!Number.isInteger(quantity) || quantity < 1 || total > 10 || total > product.stock || !product.isActive)
        throw new Error('Số lượng vượt tồn kho hoặc giới hạn 10 sản phẩm.')
      if (authenticated) return request('/cart/items', 'POST', { productId: product.id, quantity })
      const items = current.current.items.filter(x => x.productId !== product.id)
      return calculate([...items, { product, quantity: total }])
    }, 'Đã thêm sản phẩm vào giỏ hàng.')
  }
  function update(id: number, quantity: number) {
    if (checking || (locked.current && pending.current !== id)) return
    const line = current.current.items.find(x => x.productId === id)
    if (!line) return
    if (!Number.isInteger(quantity) || quantity < 1 || quantity > Math.min(10, line.product.stock)) {
      setError('Số lượng vượt tồn kho hoặc giới hạn 1–10.')
      return
    }
    lock(true); pending.current = id; setPendingId(id); setError(''); setNotice('')
    setData(calculate(current.current.items.map(x => x.productId === id ? { ...x, quantity } : x)))
    clearTimeout(timer.current)
    timer.current = setTimeout(async () => {
      pending.current = null; setPendingId(null)
      try {
        const next = authenticated ? await request('/cart/items/' + id, 'PUT', { quantity })
          : calculate(current.current.items.map(x => x.productId === id ? { ...x, quantity } : x))
        commit(next)
      } catch (cause) {
        if (alive.current) { setData(current.current); setError(authErrorMessage(cause, 'Không cập nhật được số lượng.')) }
      } finally { if (alive.current) lock(false) }
    }, 400)
  }
  return <CartContext.Provider value={{
    ...data, busy: busy || checking, pendingId, error, notice, add, update,
    remove: (id) => mutate(async () => authenticated ? request('/cart/items/' + id, 'DELETE')
      : calculate(current.current.items.filter(x => x.productId !== id)), 'Đã xóa sản phẩm.'),
    retry: () => { initialization.current = null; setAttempt(x => x + 1) },
    completeOrder: () => {
      clearTimeout(timer.current)
      pending.current = null
      setPendingId(null)
      commit(calculate([]))
      setNotice('')
      setError('')
      lock(false)
    },
  }}>
    {children}
    {notice && <div role="status" className="fixed bottom-4 right-4 z-50 rounded-lg bg-teal-900 p-4 text-white shadow-lg">{notice}</div>}
    {error && <div role="alert" className="fixed bottom-4 left-4 z-50 max-w-sm rounded-lg bg-red-100 p-4 text-red-900">{error}</div>}
  </CartContext.Provider>
}
export function useCart() {
  const cart = useContext(CartContext)
  if (!cart) throw new Error('Cần CartProvider.')
  return cart
}

