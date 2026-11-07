import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, expect, it, vi } from 'vitest'
import { App } from '../App'
import { TOKEN_KEY } from '../auth/client'
import { server } from './server'

const base = 'http://localhost:5173/api'
const ok = (data: unknown) => HttpResponse.json({ success: true, data, message: '' })
const product = { id: 1, name: 'Vợt Yonex', slug: 'yonex', price: 1000000, discountPrice: 800000, stock: 10, isActive: true, imageUrl: null,
  weight: '4U', stiffness: '', balancePoint: '', description: '', brandId: 1, categoryId: 1, brand: { id: 1, name: 'Yonex', logo: null }, category: { id: 1, name: 'Vợt', slug: 'vot' } }
beforeEach(() => {
  localStorage.setItem(TOKEN_KEY, 'token')
  server.use(
    http.get(base + '/auth/me', () => ok({ id: 2, fullName: 'An', email: 'an@example.com', role: 'Customer' })),
    http.get(base + '/products/yonex', () => ok(product)),
    http.get(base + '/cart', () => ok({ items: [{ productId: 1, product, quantity: 1, unitPrice: 800000, lineTotal: 800000, isAvailable: true }], totalAmount: 800000 })),
  )
})
function open(path: string) {
  render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>)
  return userEvent.setup()
}
it('review chỉ hiện form khi server xác nhận đủ điều kiện, chọn sao bắt buộc và gửi cập nhật danh sách', async () => {
  let posted = false
  const payload = vi.fn()
  server.use(
    http.get(base + '/products/1/reviews/eligibility', () => ok({ canReview: !posted, reason: 'Bạn đã đánh giá sản phẩm này.' })),
    http.get(base + '/products/1/reviews', () => ok({ items: posted ? [{ id: 1, userId: 2, productId: 1, rating: 5, comment: 'Vợt rất tốt' }] : [], page: 1, totalPages: 1, totalItems: posted ? 1 : 0, averageRating: posted ? 5 : 0 })),
    http.post(base + '/products/1/reviews', async ({ request }) => { payload(await request.json()); posted = true; return ok({ id: 1 }) }),
  )
  const user = open('/products/yonex')
  await user.click(await screen.findByRole('button', { name: 'Gửi đánh giá' }))
  expect(await screen.findByText('Vui lòng chọn số sao.')).toBeVisible()
  expect(payload).not.toHaveBeenCalled()
  await user.click(screen.getByRole('radio', { name: '5 sao' }))
  await user.type(screen.getByLabelText('Nhận xét'), 'Vợt rất tốt')
  await user.click(screen.getByRole('button', { name: 'Gửi đánh giá' }))
  expect(await screen.findByText('Vợt rất tốt')).toBeVisible()
  expect(await screen.findByText('Bạn đã đánh giá sản phẩm này.')).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Gửi đánh giá' })).not.toBeInTheDocument()
  expect(payload).toHaveBeenCalledExactlyOnceWith({ rating: 5, comment: 'Vợt rất tốt' })
  expect(screen.getAllByLabelText('5.0 trên 5 sao')).toHaveLength(2)
})
it('review chưa mua hiện lời giải thích và không có form', async () => {
  open('/products/yonex')
  expect(await screen.findByText('Bạn chưa hoàn tất đơn mua sản phẩm.')).toBeVisible()
  expect(screen.queryByLabelText('Nhận xét')).not.toBeInTheDocument()
})
it('khách chưa đăng nhập không được gửi review', async () => {
  localStorage.clear()
  open('/products/yonex')
  expect(await screen.findByText('Đăng nhập và hoàn tất đơn mua sản phẩm để đánh giá.')).toBeVisible()
  expect(screen.queryByLabelText('Nhận xét')).not.toBeInTheDocument()
})
it('coupon hợp lệ giảm tổng, xóa mã trả tổng về ban đầu', async () => {
  const payload = vi.fn()
  server.use(http.post(base + '/coupons/validate', async ({ request }) => { payload(await request.json()); return ok({ code: 'SALE', discountAmount: 100000 }) }))
  const user = open('/cart')
  await user.type(await screen.findByLabelText('Mã giảm giá'), 'sale')
  await user.click(screen.getByRole('button', { name: 'Áp dụng' }))
  expect(await screen.findByText('Tổng tiền: 700.000 ₫')).toBeVisible()
  expect(payload).toHaveBeenCalledExactlyOnceWith({ code: 'sale', subtotal: 800000 })
  await user.click(screen.getByRole('button', { name: 'Xóa mã' }))
  expect(screen.getByText('Tổng tiền: 800.000 ₫')).toBeVisible()
})
it.each(['Mã giảm giá đã hết hạn.', 'Đơn hàng chưa đạt giá trị tối thiểu.'])('coupon lỗi: %s', async message => {
  server.use(http.post(base + '/coupons/validate', () => HttpResponse.json({ success: false, message }, { status: 400 })))
  const user = open('/cart')
  await user.type(await screen.findByLabelText('Mã giảm giá'), 'BAD')
  await user.click(screen.getByRole('button', { name: 'Áp dụng' }))
  expect(await screen.findByText(message)).toBeVisible()
  expect(screen.getByText('Tổng tiền: 800.000 ₫')).toBeVisible()
})
it('coupon đi từ giỏ sang checkout và chỉ gửi code, không gửi giá giảm từ client', async () => {
  const payload = vi.fn()
  server.use(
    http.post(base + '/coupons/validate', () => ok({ code: 'SALE', discountAmount: 100000 })),
    http.post(base + '/orders', async ({ request }) => { payload(await request.json()); return HttpResponse.json({ success: false, message: 'Đã kiểm tra payload' }, { status: 400 }) }),
  )
  const user = open('/cart')
  await user.type(await screen.findByLabelText('Mã giảm giá'), 'sale')
  await user.click(screen.getByRole('button', { name: 'Áp dụng' }))
  await screen.findByText('Tổng tiền: 700.000 ₫')
  await user.click(screen.getByRole('button', { name: 'Thanh toán' }))
  await user.type(await screen.findByLabelText('Số điện thoại'), '0901234567')
  await user.type(screen.getByLabelText('Địa chỉ giao hàng'), 'Hà Nội')
  await user.click(screen.getByRole('button', { name: 'Đặt hàng' }))
  await screen.findByText('Đã kiểm tra payload')
  expect(payload).toHaveBeenCalledWith({ fullName: 'An', phone: '0901234567', shippingAddress: 'Hà Nội', note: '', paymentMethod: 'COD', couponCode: 'SALE' })
})
it('thay đổi số lượng vô hiệu hóa mã cũ', async () => {
  server.use(
    http.post(base + '/coupons/validate', () => ok({ code: 'SALE', discountAmount: 100000 })),
    http.put(base + '/cart/items/1', () => ok({ items: [{ productId: 1, product, quantity: 2, unitPrice: 800000, lineTotal: 1600000, isAvailable: true }], totalAmount: 1600000 })),
  )
  const user = open('/cart')
  await user.type(await screen.findByLabelText('Mã giảm giá'), 'sale')
  await user.click(screen.getByRole('button', { name: 'Áp dụng' }))
  await screen.findByText('Tổng tiền: 700.000 ₫')
  fireEvent.click(screen.getByRole('button', { name: 'Tăng Vợt Yonex' }))
  await waitFor(() => expect(screen.queryByRole('button', { name: 'Xóa mã' })).not.toBeInTheDocument())
  expect(await screen.findByText('Tổng tiền: 1.600.000 ₫')).toBeVisible()
})

