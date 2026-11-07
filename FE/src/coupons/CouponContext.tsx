import { createContext, useContext, useEffect, useRef, useState, type ReactNode } from 'react'
import { useAuth } from '../auth/AuthContext'
import { useCart } from '../cart/CartContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'

interface Applied { code: string; discountAmount: number; signature: string }
interface CouponValue { code: string; discount: number; total: number; busy: boolean; error: string; apply: (code: string) => Promise<void>; clear: () => void }
const Context = createContext<CouponValue | null>(null)
export function CouponProvider({ children }: { children: ReactNode }) {
  const { api, user } = useAuth()
  const cart = useCart()
  const signature = JSON.stringify([user?.id, cart.items.map(x => [x.productId, x.quantity, x.unitPrice])])
  const latest = useRef(signature)
  latest.current = signature
  const revision = useRef(0)
  const [applied, setApplied] = useState<Applied | null>(null)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState('')
  useEffect(() => { revision.current++; setApplied(null); setError(''); setBusy(false) }, [signature])
  const valid = applied?.signature === signature ? applied : null
  function clear() { revision.current++; setApplied(null); setError(''); setBusy(false) }
  async function apply(input: string) {
    const code = input.trim()
    if (!code) { setError('Vui lòng nhập mã giảm giá.'); return }
    const request = ++revision.current
    setBusy(true); setError(''); setApplied(null)
    try {
      const response = await api.post<ApiResponse<{ code: string; discountAmount: number }>>('/coupons/validate', { code, subtotal: cart.totalAmount })
      if (!response.data.success) throw new Error(response.data.message)
      // Bỏ kết quả nếu giỏ hoặc mã đã thay đổi trong lúc gửi.
      if (request === revision.current && signature === latest.current) setApplied({ ...response.data.data, signature })
    } catch (cause) {
      if (request === revision.current) setError(authErrorMessage(cause, 'Không áp dụng được mã.'))
    } finally { if (request === revision.current) setBusy(false) }
  }
  return <Context.Provider value={{ code: valid?.code ?? '', discount: valid?.discountAmount ?? 0,
    total: Math.max(0, cart.totalAmount - (valid?.discountAmount ?? 0)), busy, error, apply, clear }}>{children}</Context.Provider>
}
export function useCoupon() {
  const context = useContext(Context)
  if (!context) throw new Error('Cần CouponProvider.')
  return context
}

