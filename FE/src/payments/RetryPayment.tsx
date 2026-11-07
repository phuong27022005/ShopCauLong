import { useEffect, useRef, useState } from 'react'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { Order } from '../orders/types'
import { paymentDeadline, startVnpay } from './vnpay'

export function RetryPayment({ order }: { order: Order }) {
  const { api } = useAuth()
  const [now, setNow] = useState(Date.now)
  const [busy, setBusy] = useState(false)
  const lock = useRef(false)
  const [error, setError] = useState('')
  const deadline = paymentDeadline(order.paymentExpiredAt)
  useEffect(() => {
    setNow(Date.now())
    if (deadline <= Date.now()) return
    const interval = window.setInterval(() => {
      const time = Date.now()
      setNow(time)
      if (time >= deadline) window.clearInterval(interval)
    }, 1000)
    return () => window.clearInterval(interval)
  }, [deadline])
  if (order.paymentMethod !== 'VNPAY' || order.status !== 'Pending' ||
      !['Unpaid', 'Failed'].includes(order.paymentStatus ?? 'Unpaid')) return null
  const seconds = Math.max(0, Math.ceil((deadline - now) / 1000))
  async function pay() {
    if (lock.current || Date.now() >= deadline) return
    lock.current = true; setBusy(true); setError('')
    try { await startVnpay(api, order.id) }
    catch (cause) { setError(authErrorMessage(cause, 'Không tạo được thanh toán.')); lock.current = false; setBusy(false) }
  }
  return <section className="space-y-2">
    {seconds > 0 ? <>
      <p>Thời gian thanh toán còn lại: <span role="timer">{Math.floor(seconds / 60)}:{String(seconds % 60).padStart(2, '0')}</span></p>
      <button disabled={busy} onClick={() => void pay()} className="rounded bg-teal-800 px-4 py-2 text-white disabled:opacity-50">
        {busy ? 'Đang chuyển đến VNPay…' : 'Thanh toán lại'}
      </button>
    </> : <p className="text-red-800">Đã hết hạn thanh toán.</p>}
    {error && <p role="alert" className="text-red-800">{error}</p>}
  </section>
}
