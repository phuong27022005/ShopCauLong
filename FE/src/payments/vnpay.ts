import type { AxiosInstance } from 'axios'
import type { ApiResponse } from '../auth/types'

export const paymentNavigation = {
  redirect(url: string) { window.location.assign(url) },
}
export async function startVnpay(api: AxiosInstance, orderId: number) {
  const { data } = await api.post<ApiResponse<{ paymentUrl: string }>>('/payments/vnpay/create', { orderId })
  if (!data.success) throw new Error(data.message)
  const url = new URL(data.data.paymentUrl)
  if (url.protocol !== 'https:' || url.hostname !== 'sandbox.vnpayment.vn' || url.username || url.password)
    throw new Error('Địa chỉ thanh toán VNPay không hợp lệ.')
  paymentNavigation.redirect(url.href)
}
export function paymentDeadline(value: string | null | undefined) {
  if (!value) return 0
  const milliseconds = Date.parse(/Z$|[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z')
  return Number.isFinite(milliseconds) ? milliseconds : 0
}
