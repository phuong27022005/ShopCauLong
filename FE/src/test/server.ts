import { http, HttpResponse } from 'msw'
import { setupServer } from 'msw/node'

const ok = (data: unknown) => HttpResponse.json({ success: true, data, message: '' })
export const server = setupServer(
  http.get('http://localhost:5173/api/cart', () => ok({ items: [], totalAmount: 0 })),
  http.get('http://localhost:5173/api/products/:id/reviews', () => ok({ items: [], page: 1, totalPages: 0, totalItems: 0, averageRating: 0 })),
  http.get('http://localhost:5173/api/products/:id/reviews/eligibility', () => ok({ canReview: false, reason: 'Bạn chưa hoàn tất đơn mua sản phẩm.' })),
  http.get('http://localhost:5173/api/admin/stats/overview', () => ok({ totalRevenue: 0, newCustomers: 0, lowStockProducts: 0, ordersByStatus: [] })),
  http.get('http://localhost:5173/api/admin/stats/revenue', () => ok([])),
  http.get('http://localhost:5173/api/admin/stats/top-products', () => ok([])),
)
export const authUrl = (route: string) => `http://localhost:5173/api/auth/${route}`

