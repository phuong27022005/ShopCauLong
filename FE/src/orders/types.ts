export interface Order {
  paymentStatus: 'Unpaid' | 'Paid' | 'Failed' | 'Refunded'
  paymentExpiredAt: string | null
  id: number; code: string; status: string; totalAmount: number; fullName: string | null
  shippingAddress: string; phone: string; paymentMethod: string; note: string | null; createdAt: string
  items: { productId: number; productName: string; quantity: number; unitPrice: number; lineTotal: number }[]
}
export interface OrderPage { items: Order[]; page: number; pageSize: number; totalItems: number; totalPages: number }
export function orderDate(value: string) {
  // EF trả UTC đôi khi không có hậu tố Z.
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeZone: 'Asia/Ho_Chi_Minh' })
    .format(new Date(/Z$|[+-]\d{2}:\d{2}$/.test(value) ? value : value + 'Z'))
}

