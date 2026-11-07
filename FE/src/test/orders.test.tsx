import { act, fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, expect, it, vi } from 'vitest'
import { App } from '../App'
import { TOKEN_KEY } from '../auth/client'
import { server } from './server'

const ok = (data: unknown) => HttpResponse.json({ success: true, data, message: '' })
const order = {
  id: 1, code: 'BS20261106-0001', status: 'Pending', totalAmount: 800000,
  fullName: 'Nguyễn An', phone: '0901234567', shippingAddress: 'Hà Nội', paymentMethod: 'COD',
  note: '', createdAt: '2026-11-06T01:00:00Z',
  items: [{ productId: 1, productName: 'Vợt Yonex', quantity: 1, unitPrice: 800000, lineTotal: 800000 }],
}
beforeEach(() => {
  localStorage.setItem(TOKEN_KEY, 'token')
  server.use(
    http.get('http://localhost:5173/api/auth/me', () => ok({ id: 2, fullName: '', email: 'a@example.com', role: 'Customer' })),
    http.get('http://localhost:5173/api/cart', () => ok({
      items: [{ productId: 1, quantity: 1, unitPrice: 800000, lineTotal: 800000, isAvailable: true,
        product: { id: 1, name: 'Vợt Yonex', slug: 'vot-yonex', imageUrl: null, price: 1000000, discountPrice: 800000, stock: 2, isActive: true } }],
      totalAmount: 800000,
    })),
    http.get('http://localhost:5173/api/orders/1', () => ok(order)),
  )
})
function open(path = '/checkout') {
  render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>)
  return userEvent.setup()
}
async function fill(user: ReturnType<typeof userEvent.setup>) {
  await user.type(await screen.findByLabelText('Họ tên người nhận'), 'Nguyễn An')
  await user.type(screen.getByLabelText('Số điện thoại'), '0901234567')
  await user.type(screen.getByLabelText('Địa chỉ giao hàng'), 'Hà Nội')
}

it('thiếu trường hoặc số điện thoại sai không gọi API', async () => {
  const submit = vi.fn(() => ok(order))
  server.use(http.post('http://localhost:5173/api/orders', submit))
  const user = open()
  const button = await screen.findByRole('button', { name: 'Đặt hàng' })
  await waitFor(() => expect(button).toBeEnabled())
  await user.click(button)
  expect(await screen.findByText('Vui lòng nhập họ tên.')).toBeVisible()
  expect(screen.getByText('Vui lòng nhập địa chỉ.')).toBeVisible()
  await fill(user)
  await user.clear(screen.getByLabelText('Số điện thoại'))
  await user.type(screen.getByLabelText('Số điện thoại'), '12345')
  await user.click(button)
  expect(await screen.findByText('Số điện thoại Việt Nam không hợp lệ.')).toBeVisible()
  expect(submit).not.toHaveBeenCalled()
})

it('gửi đúng payload, chống bấm đúp, chuyển success và xóa giỏ', async () => {
  const payload = vi.fn()
  let release!: () => void
  const gate = new Promise<void>(resolve => { release = resolve })
  server.use(http.post('http://localhost:5173/api/orders', async ({ request }) => {
    payload(await request.json())
    await gate
    return ok(order)
  }))
  const user = open()
  await fill(user)
  const button = screen.getByRole('button', { name: 'Đặt hàng' })
  fireEvent.click(button)
  fireEvent.click(button)
  await waitFor(() => expect(payload).toHaveBeenCalledTimes(1))
  expect(button).toBeDisabled()
  await act(async () => release())
  expect(await screen.findByRole('heading', { name: 'Đặt hàng thành công' })).toBeVisible()
  expect(screen.getByRole('link', { name: 'Giỏ hàng, 0 sản phẩm' })).toBeVisible()
  expect(payload).toHaveBeenCalledExactlyOnceWith({
    fullName: 'Nguyễn An', phone: '0901234567', shippingAddress: 'Hà Nội', note: '', paymentMethod: 'COD',
  })
})

it.each([
  [400, 'Sản phẩm Vợt Yonex không đủ tồn kho.'],
  [409, 'Giá sản phẩm Vợt Yonex đã thay đổi.'],
])('hiện lỗi BE %s và link về giỏ', async (status, message) => {
  server.use(http.post('http://localhost:5173/api/orders', () =>
    HttpResponse.json({ success: false, data: null, message }, { status })))
  const user = open()
  await fill(user)
  await user.click(screen.getByRole('button', { name: 'Đặt hàng' }))
  expect(await screen.findByRole('alert')).toHaveTextContent(message)
  expect(screen.getByRole('link', { name: 'Về giỏ hàng để kiểm tra' })).toHaveAttribute('href', '/cart')
  expect(screen.getByRole('link', { name: 'Giỏ hàng, 1 sản phẩm' })).toBeVisible()
})

it('danh sách loading, dữ liệu, badge và phân trang', async () => {
  const requests = vi.fn()
  server.use(http.get('http://localhost:5173/api/orders', async ({ request }) => {
    const page = Number(new URL(request.url).searchParams.get('page'))
    requests(page)
    await delay(60)
    return ok({ items: [{ ...order, status: page === 1 ? 'Pending' : 'Completed' }], page, pageSize: 12, totalItems: 13, totalPages: 2 })
  }))
  const user = open('/orders')
  expect(await screen.findByText('Đang tải đơn hàng…')).toBeInTheDocument()
  expect(await screen.findByRole('link', { name: order.code })).toHaveAttribute('href', '/orders/1')
  expect(screen.getByText('Chờ xác nhận')).toHaveClass('bg-amber-100')
  await user.click(screen.getByRole('button', { name: 'Trang sau' }))
  expect(await screen.findByText('Hoàn thành')).toHaveClass('bg-green-100')
  expect(requests.mock.calls).toEqual([[1], [2]])
})

it('danh sách rỗng', async () => {
  server.use(http.get('http://localhost:5173/api/orders', () => ok({ items: [], page: 1, pageSize: 12, totalItems: 0, totalPages: 0 })))
  open('/orders')
  expect(await screen.findByText('Chưa có đơn hàng trên trang này.')).toBeVisible()
})

it('hủy đơn yêu cầu xác nhận và cập nhật trạng thái', async () => {
  const cancel = vi.fn(() => ok({ ...order, status: 'Cancelled' }))
  server.use(http.post('http://localhost:5173/api/orders/1/cancel', cancel))
  const user = open('/orders/1')
  await user.click(await screen.findByRole('button', { name: 'Hủy đơn' }))
  const dialog = screen.getByRole('dialog')
  expect(cancel).not.toHaveBeenCalled()
  await user.click(within(dialog).getByRole('button', { name: 'Giữ đơn' }))
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  await user.click(screen.getByRole('button', { name: 'Hủy đơn' }))
  await user.click(screen.getByRole('button', { name: 'Xác nhận hủy' }))
  expect(await screen.findByText('Đã hủy')).toHaveClass('bg-red-100')
  expect(screen.queryByRole('button', { name: 'Hủy đơn' })).not.toBeInTheDocument()
  expect(cancel).toHaveBeenCalledTimes(1)
})

it('đơn Shipping ẩn nút hủy', async () => {
  server.use(http.get('http://localhost:5173/api/orders/1', () => ok({ ...order, status: 'Shipping' })))
  open('/orders/1')
  expect(await screen.findByRole('heading', { name: 'Chi tiết đơn hàng' })).toBeVisible()
  expect(screen.queryByRole('button', { name: 'Hủy đơn' })).not.toBeInTheDocument()
})

