import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it } from 'vitest'
import { App } from '../App'
import { readComparison } from '../shopping/ShoppingTools'
import { server } from './server'
import { TOKEN_KEY } from '../auth/client'

const url = (path: string) => `http://localhost:5173/api/${path}`
const ok = (data: unknown, message = 'Thành công.') => HttpResponse.json({ success: true, data, message })
const products = [1, 2, 3, 4].map(id => ({ id, name: `Vợt ${id}`, slug: `vot-${id}`, price: id * 1000000, discountPrice: null, imageUrl: null, brandName: 'Yonex', stock: 5, brand: { id: 1, name: 'Yonex' }, category: { id: 1, name: 'Tấn công' }, weight: '4U', stiffness: 'Cứng', balancePoint: 'Nặng đầu', skillLevel: 'Trung cấp', isActive: true }))
function open(path: string) { render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>); return userEvent.setup() }
function catalog() {
  server.use(http.get(url('products'), () => ok({ items: products, page: 1, pageSize: 12, totalItems: 4, totalPages: 1 })), http.get(url('brands'), () => ok([])), http.get(url('categories'), () => ok([])))
}

describe('So sánh và yêu thích', () => {
  it('khôi phục an toàn danh sách so sánh từ dữ liệu hỏng', () => {
    localStorage.setItem('badminton-shop.compare', '{bad json')
    expect(readComparison()).toEqual([])
    localStorage.setItem('badminton-shop.compare', '[1,1,-1,"2",2,3,4]')
    expect(readComparison()).toEqual([1, 2, 3])
  })
  it('giới hạn ba sản phẩm và hiển thị bảng thông số', async () => {
    catalog()
    server.use(http.get(url('products/compare'), ({ request }) => {
      const ids = new URL(request.url).searchParams.getAll('ids').map(Number)
      return ok(products.filter(p => ids.includes(p.id)))
    }))
    const user = open('/products')
    const cards = await screen.findAllByRole('article')
    for (const card of cards) await user.click(within(card).getByRole('button', { name: /^So sánh$/ }))
    expect(await screen.findByText(/Chỉ so sánh tối đa 3/)).toBeInTheDocument()
    await user.click(screen.getByRole('link', { name: 'Mở bảng so sánh' }))
    expect(await screen.findByRole('table')).toBeInTheDocument()
    expect(screen.getAllByText('Trung cấp')).toHaveLength(3)
    await user.click(screen.getByRole('button', { name: 'Bỏ Vợt 1' }))
    expect(await screen.findByText('Đã chọn 2/3 sản phẩm')).toBeInTheDocument()
  })
  it('chuyển khách sang đăng nhập khi lưu yêu thích', async () => {
    catalog()
    const user = open('/products')
    await user.click((await screen.findAllByRole('button', { name: '♡ Yêu thích' }))[0])
    expect(await screen.findByRole('heading', { name: 'Đăng nhập' })).toBeInTheDocument()
  })
  it('xóa yêu thích của tài khoản đang đăng nhập', async () => {
    localStorage.setItem(TOKEN_KEY, 'test-token')
    let favorites = [products[0]]
    server.use(http.get(url('auth/me'), () => ok({ id: 2, fullName: 'An', email: 'an@example.com', role: 'Customer' })),
      http.get(url('favorites'), () => ok(favorites)), http.delete(url('favorites/1'), () => { favorites = []; return ok(null) }))
    const user = open('/favorites')
    await user.click(await screen.findByRole('button', { name: '♥ Đã yêu thích' }))
    expect(await screen.findByText(/Bạn chưa lưu sản phẩm nào/)).toBeInTheDocument()
  })
})

describe('Email và mật khẩu', () => {
  it('gửi yêu cầu quên mật khẩu và hiển thị phản hồi chung', async () => {
    let sent = ''
    server.use(http.post(url('auth/forgot-password'), async ({ request }) => { sent = (await request.json() as { email: string }).email; return ok(null, 'Nếu email đã đăng ký, hướng dẫn sẽ được gửi.') }))
    const user = open('/forgot-password')
    await user.type(screen.getByLabelText('Email'), 'an@example.com')
    await user.click(screen.getByRole('button', { name: 'Quên mật khẩu' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Nếu email đã đăng ký')
    expect(sent).toBe('an@example.com')
  })
  it('không gửi reset nếu xác nhận mật khẩu không khớp', async () => {
    const user = open('/reset-password#token=' + 'A'.repeat(96))
    await user.type(screen.getByLabelText('Mật khẩu mới'), 'StrongPass1!')
    await user.type(screen.getByLabelText('Xác nhận mật khẩu'), 'Different1!')
    await user.click(screen.getByRole('button', { name: 'Đặt lại mật khẩu' }))
    expect(await screen.findByRole('alert')).toHaveTextContent('Mật khẩu xác nhận không khớp')
  })
  it('chỉ xác minh khi người dùng nhấn nút, không tự tiêu thụ token', async () => {
    let calls = 0
    server.use(http.post(url('auth/verify-email'), () => { calls++; return ok(null, 'Đã xác minh email.') }))
    const user = open('/verify-email#token=' + 'A'.repeat(96))
    expect(calls).toBe(0)
    await user.click(screen.getByRole('button', { name: 'Xác minh email' }))
    expect(await screen.findByRole('status')).toHaveTextContent('Đã xác minh email.')
    expect(calls).toBe(1)
  })
  it('hiển thị hướng dẫn khi thiếu token reset', () => {
    open('/reset-password')
    expect(screen.getByRole('alert')).toHaveTextContent('Thiếu liên kết hợp lệ')
    expect(screen.queryByRole('button', { name: 'Đặt lại mật khẩu' })).not.toBeInTheDocument()
  })
})
