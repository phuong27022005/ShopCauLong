import { act, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { App } from '../App'
import { TOKEN_KEY } from '../auth/client'
import { paymentNavigation } from '../payments/vnpay'
import { server } from './server'

const ok = (data: unknown) => HttpResponse.json({ success: true, data, message: '' })
function order(paymentStatus = 'Unpaid', seconds = 900) {
  return { id: 1, code: 'BS20261106-0001', status: 'Pending', totalAmount: 800000,
    fullName: 'Nguyễn An', phone: '0901234567', shippingAddress: 'Hà Nội', note: null,
    paymentMethod: 'VNPAY', paymentStatus, paymentExpiredAt: new Date(Date.now() + seconds * 1000).toISOString(),
    createdAt: '2026-11-06T01:00:00Z', items: [] }
}
function open(path: string) {
  render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>)
  return userEvent.setup()
}
beforeEach(() => {
  localStorage.setItem(TOKEN_KEY, 'token')
  server.use(
    http.get('http://localhost:5173/api/auth/me', () => ok({ id: 2, fullName: 'Nguyễn An', role: 'Customer' })),
    http.get('http://localhost:5173/api/orders/1', () => ok(order())),
  )
})
afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks() })

it('checkout VNPay tạo URL đúng orderId và chuyển window.location qua adapter', async () => {
  const redirect = vi.spyOn(paymentNavigation, 'redirect').mockImplementation(() => {})
  const create = vi.fn()
  server.use(
    http.get('http://localhost:5173/api/cart', () => ok({
      items: [{ productId: 1, quantity: 1, unitPrice: 800000, lineTotal: 800000, isAvailable: true,
        product: { id: 1, name: 'Vợt', slug: 'vot', imageUrl: null, price: 800000, discountPrice: null, stock: 2, isActive: true } }],
      totalAmount: 800000,
    })),
    http.post('http://localhost:5173/api/orders', () => ok(order())),
    http.post('http://localhost:5173/api/payments/vnpay/create', async ({ request }) => {
      create(await request.json())
      return ok({ paymentUrl: 'https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_TxnRef=test' })
    }),
  )
  const user = open('/checkout')
  await user.type(await screen.findByLabelText('Số điện thoại'), '0901234567')
  await user.type(screen.getByLabelText('Địa chỉ giao hàng'), 'Hà Nội')
  await user.click(screen.getByRole('radio', { name: 'VNPay' }))
  await user.click(screen.getByRole('button', { name: 'Đặt hàng' }))
  await waitFor(() => expect(redirect).toHaveBeenCalledTimes(1))
  expect(create).toHaveBeenCalledExactlyOnceWith({ orderId: 1 })
  expect(redirect).toHaveBeenCalledWith('https://sandbox.vnpayment.vn/paymentv2/vpcpay.html?vnp_TxnRef=test')
  expect(screen.getByRole('link', { name: 'Giỏ hàng, 0 sản phẩm' })).toBeVisible()
})

it.each([
  ['Paid', 'Thanh toán thành công'],
  ['Failed', 'Thanh toán thất bại'],
])('kết quả %s lấy từ server', async (status, title) => {
  server.use(http.get('http://localhost:5173/api/orders/1', () => ok(order(status))))
  open('/payment/result?orderId=1&paymentResult=failed')
  expect(await screen.findByRole('heading', { name: title })).toBeVisible()
})

it('URL giả mạo thành công nhưng Unpaid vẫn chưa thanh toán', async () => {
  open('/payment/result?orderId=1&paymentResult=success&status=Paid')
  expect(await screen.findByText('Chưa thanh toán')).toBeVisible()
  expect(screen.queryByRole('heading', { name: 'Thanh toán thành công' })).not.toBeInTheDocument()
  expect(screen.getByRole('heading', { name: 'Đang xử lý thanh toán' })).toBeVisible()
})

it('poll sau 3 giây chuyển từ Unpaid sang Paid và dừng', async () => {
  let requests = 0
  server.use(http.get('http://localhost:5173/api/orders/1', () => ok(order(++requests === 1 ? 'Unpaid' : 'Paid'))))
  open('/payment/result?orderId=1')
  expect(await screen.findByText('Chưa thanh toán')).toBeVisible()
  expect(await screen.findByRole('heading', { name: 'Thanh toán thành công' }, { timeout: 5000 })).toBeVisible()
  expect(requests).toBe(2)
}, 10000)

it('đơn quá hạn ẩn nút thanh toán lại', async () => {
  server.use(http.get('http://localhost:5173/api/orders/1', () => ok(order('Unpaid', -1))))
  open('/orders/1')
  expect(await screen.findByText('Đã hết hạn thanh toán.')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Thanh toán lại' })).not.toBeInTheDocument()
})

it('đếm ngược về 0 cập nhật giao diện', async () => {
  vi.useFakeTimers()
  const expiring = order('Unpaid', 3)
  server.use(http.get('http://localhost:5173/api/orders/1', () => ok(expiring)))
  open('/orders/1')
  await act(async () => {
    for (let i = 0; i < 5; i++) await vi.advanceTimersByTimeAsync(50)
  })
  await vi.waitFor(() => expect(screen.getByRole('timer')).toBeInTheDocument())
  await act(() => vi.advanceTimersByTimeAsync(4000))
  expect(screen.getByText('Đã hết hạn thanh toán.')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Thanh toán lại' })).not.toBeInTheDocument()
})

it('đơn Failed còn hạn thanh toán lại và badge tách biệt', async () => {
  const redirect = vi.spyOn(paymentNavigation, 'redirect').mockImplementation(() => {})
  server.use(
    http.get('http://localhost:5173/api/orders/1', () => ok(order('Failed'))),
    http.post('http://localhost:5173/api/payments/vnpay/create', () => ok({ paymentUrl: 'https://sandbox.vnpayment.vn/paymentv2/vpcpay.html' })),
  )
  const user = open('/orders/1')
  expect(await screen.findByText('Thanh toán thất bại')).toBeVisible()
  expect(screen.getAllByText('Chờ xác nhận').length).toBeGreaterThan(0)
  await user.click(screen.getByRole('button', { name: 'Thanh toán lại' }))
  await waitFor(() => expect(redirect).toHaveBeenCalledTimes(1))
})

