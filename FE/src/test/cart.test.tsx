import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, it, vi } from 'vitest'
import { App } from '../App'
import { CART_KEY } from '../cart/CartContext'
import { TOKEN_KEY } from '../auth/client'
import { server } from './server'

const product = { id: 1, name: 'Vợt mẫu', slug: 'vot-mau', imageUrl: null, price: 1000000,
  discountPrice: 800000, stock: 2, isActive: true }
const line = { productId: 1, product, quantity: 1, unitPrice: 800000, lineTotal: 800000, isAvailable: true }
const ok = (data: unknown) => HttpResponse.json({ success: true, data, message: '' })
const user = { id: 2, fullName: 'Khách', email: 'customer@example.com', role: 'Customer' }
function seed() {
  localStorage.setItem(CART_KEY, JSON.stringify([{ product, quantity: 1 }]))
  server.use(http.get('http://localhost:5173/api/products/vot-mau', () => ok(product)))
}
function open(path = '/cart') {
  render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>)
  return userEvent.setup()
}
afterEach(() => vi.useRealTimers())

it('thêm trùng cộng dồn, tăng badge, toast và lưu localStorage', async () => {
  server.use(http.get('http://localhost:5173/api/products/vot-mau', () => ok({
    ...product, brand: { name: 'Yonex' }, category: { name: 'Vợt' }, weight: '4U',
    stiffness: 'Cứng', balancePoint: 'Cân bằng', description: '', createdAt: '',
  })))
  const events = open('/products/vot-mau')
  const button = await screen.findByRole('button', { name: 'Thêm vào giỏ' })
  await events.click(button)
  expect(await screen.findByRole('link', { name: 'Giỏ hàng, 1 sản phẩm' })).toBeVisible()
  expect(screen.getByRole('status')).toHaveTextContent('Đã thêm sản phẩm')
  await events.click(button)
  expect(screen.getByRole('link', { name: 'Giỏ hàng, 2 sản phẩm' })).toBeVisible()
  expect(JSON.parse(localStorage.getItem(CART_KEY)!)[0].quantity).toBe(2)
})

it('giá giảm và giới hạn tồn kho; không nhận số lượng vượt tồn', async () => {
  seed()
  const events = open()
  await waitFor(() => expect(screen.queryByText('Đang cập nhật giỏ hàng…')).not.toBeInTheDocument())
  expect(screen.getByText('Tổng tiền: 800.000 ₫')).toBeVisible()
  await events.click(screen.getByRole('button', { name: 'Tăng Vợt mẫu' }))
  expect(screen.getByText('Tổng tiền: 1.600.000 ₫')).toBeVisible()
  await events.click(screen.getByRole('button', { name: 'Tăng Vợt mẫu' }))
  expect(screen.getByRole('alert')).toHaveTextContent('vượt tồn kho')
  expect(screen.getByText('Tổng tiền: 1.600.000 ₫')).toBeVisible()
})

it('login gọi merge và chỉ xóa giỏ local sau thành công', async () => {
  seed()
  const merge = vi.fn()
  server.use(
    http.post('http://localhost:5173/api/auth/login', () => ok({ user, accessToken: 'token' })),
    http.post('http://localhost:5173/api/cart/merge', async ({ request }) => {
      merge(await request.json(), request.headers.get('authorization'))
      return ok({ items: [line], totalAmount: 800000 })
    }),
  )
  const events = open('/login')
  await events.type(screen.getByLabelText('Email'), user.email)
  await events.type(screen.getByLabelText('Mật khẩu'), 'Password!123')
  await events.click(screen.getByRole('button', { name: 'Đăng nhập' }))
  await waitFor(() => expect(localStorage.getItem(CART_KEY)).toBeNull())
  expect(merge).toHaveBeenCalledExactlyOnceWith({ items: [{ productId: 1, quantity: 1 }] }, 'Bearer token')
  expect(screen.getByRole('link', { name: 'Giỏ hàng, 1 sản phẩm' })).toBeVisible()
})

it('merge lỗi giữ giỏ local', async () => {
  seed()
  localStorage.setItem(TOKEN_KEY, 'token')
  server.use(
    http.get('http://localhost:5173/api/auth/me', () => ok(user)),
    http.post('http://localhost:5173/api/cart/merge', () =>
      HttpResponse.json({ success: false, data: null, message: 'Không đủ hàng.' }, { status: 400 })),
  )
  open()
  expect(await screen.findByRole('alert')).toHaveTextContent('Không đủ hàng.')
  expect(localStorage.getItem(CART_KEY)).not.toBeNull()
})

it('debounce số lượng và rollback khi API lỗi', async () => {
  localStorage.setItem(TOKEN_KEY, 'token')
  const update = vi.fn()
  server.use(
    http.get('http://localhost:5173/api/auth/me', () => ok(user)),
    http.get('http://localhost:5173/api/cart', () => ok({ items: [line], totalAmount: 800000 })),
    http.put('http://localhost:5173/api/cart/items/1', async ({ request }) => {
      update(await request.json())
      return HttpResponse.json({ success: false, data: null, message: 'Tồn kho đã thay đổi.' }, { status: 400 })
    }),
  )
  open()
  await screen.findByText('Tổng tiền: 800.000 ₫')
  vi.useFakeTimers()
  fireEvent.click(screen.getByRole('button', { name: 'Tăng Vợt mẫu' }))
  expect(screen.getByText('Tổng tiền: 1.600.000 ₫')).toBeVisible()
  await act(() => vi.advanceTimersByTimeAsync(399))
  expect(update).not.toHaveBeenCalled()
  await act(() => vi.advanceTimersByTimeAsync(1))
  vi.useRealTimers()
  expect(await screen.findByRole('alert')).toHaveTextContent('Tồn kho đã thay đổi.')
  expect(screen.getByText('Tổng tiền: 800.000 ₫')).toBeVisible()
  expect(update).toHaveBeenCalledExactlyOnceWith({ quantity: 2 })
})

it('item không khả dụng chặn thanh toán', async () => {
  localStorage.setItem(TOKEN_KEY, 'token')
  server.use(
    http.get('http://localhost:5173/api/auth/me', () => ok(user)),
    http.get('http://localhost:5173/api/cart', () => ok({ items: [{ ...line, isAvailable: false }], totalAmount: 800000 })),
  )
  open()
  expect(await screen.findByRole('button', { name: 'Thanh toán' })).toBeDisabled()
  expect(screen.getByText(/Sản phẩm hết hàng/)).toBeVisible()
})

it('giỏ rỗng có link về sản phẩm', async () => {
  open()
  expect(await screen.findByText('Giỏ hàng đang trống.')).toBeVisible()
  expect(screen.getByRole('link', { name: 'Tiếp tục mua sắm' })).toHaveAttribute('href', '/products')
})

it('khách bấm thanh toán chuyển đến đăng nhập', async () => {
  seed()
  const events = open()
  await waitFor(() => expect(screen.getByRole('button', { name: 'Thanh toán' })).toBeEnabled())
  await events.click(screen.getByRole('button', { name: 'Thanh toán' }))
  expect(await screen.findByRole('heading', { name: 'Đăng nhập' })).toBeVisible()
})

