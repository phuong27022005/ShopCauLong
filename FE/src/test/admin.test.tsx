import { fireEvent, render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, expect, it, vi } from 'vitest'
import { App } from '../App'
import { TOKEN_KEY } from '../auth/client'
import { server } from './server'

const base = 'http://localhost:5173/api'
const ok = (data: unknown) => HttpResponse.json({ success: true, data, message: '' })
const product = { id: 1, name: 'Vợt Yonex', slug: 'yonex', price: 1000000, discountPrice: null, stock: 10, isActive: true, imageUrl: null,
  weight: '4U', stiffness: '', balancePoint: '', description: '', brandId: 1, categoryId: 1, brand: { id: 1, name: 'Yonex' }, category: { id: 1, name: 'Vợt' } }
beforeEach(() => {
  localStorage.setItem(TOKEN_KEY, 'token')
  server.use(
    http.get(base + '/auth/me', () => ok({ id: 1, fullName: 'Admin', email: 'admin@example.com', role: 'Admin' })),
    http.get(base + '/brands', () => ok([{ id: 1, name: 'Yonex' }])),
    http.get(base + '/categories', () => ok([{ id: 1, name: 'Vợt' }])),
    http.get(base + '/admin/products', () => ok({ items: [product], totalPages: 1 })),
  )
})
function open(path = '/admin') {
  render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>)
  return userEvent.setup({ applyAccept: false })
}
it('dashboard render số liệu, biểu đồ Recharts và đổi khoảng ngày gọi lại API', async () => {
  const requests = vi.fn()
  server.use(
    http.get(base + '/admin/stats/overview', ({ request }) => { requests(new URL(request.url).search); return ok({ totalRevenue: 351000, newCustomers: 3, lowStockProducts: 2, ordersByStatus: [{ status: 'Completed', count: 2 }] }) }),
    http.get(base + '/admin/stats/revenue', () => ok([{ period: '2026-01-01', revenue: 351000 }])),
    http.get(base + '/admin/stats/top-products', () => ok([{ productId: 1, name: 'Yonex bán chạy', quantitySold: 8 }])),
  )
  open()
  expect(await screen.findByText('Yonex bán chạy')).toBeVisible()
  expect(await screen.findByText('351.000 ₫', { selector: 'strong' })).toBeVisible()
  const chart = await screen.findByLabelText('Biểu đồ doanh thu')
  await waitFor(() => expect(chart.querySelector('svg.recharts-surface')).toBeInTheDocument())
  expect(within(chart).getByText('351.000 ₫')).toBeInTheDocument()
  fireEvent.change(screen.getByLabelText('Từ ngày'), { target: { value: '2026-01-01' } })
  fireEvent.change(screen.getByLabelText('Đến ngày'), { target: { value: '2026-01-31' } })
  await waitFor(() => expect(requests).toHaveBeenCalledWith('?from=2026-01-01&to=2026-01-31'))
})
it('dashboard rỗng hiển thị lời giải thích', async () => {
  open()
  expect(await screen.findByText('Chưa có doanh thu trong khoảng ngày này.')).toBeVisible()
  expect(await screen.findByText('Chưa có sản phẩm bán chạy.')).toBeVisible()
})
it('dashboard từ ngày lớn hơn đến ngày chặn query sai', async () => {
  const requests = vi.fn((request: Request) => new URL(request.url).search)
  server.use(http.get(base + '/admin/stats/revenue', ({ request }) => { requests(request); return ok([]) }))
  open()
  fireEvent.change(await screen.findByLabelText('Từ ngày'), { target: { value: '2026-02-01' } })
  fireEvent.change(screen.getByLabelText('Đến ngày'), { target: { value: '2026-01-01' } })
  expect(await screen.findByText('Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.')).toBeVisible()
  expect(requests.mock.calls.every(([info]) => !new URL(info.url).searchParams.has('to'))).toBe(true)
})
it('Customer vào admin bị chuyển hướng', async () => {
  const adminApi = vi.fn(() => ok([]))
  server.use(
    http.get(base + '/auth/me', () => ok({ id: 2, fullName: 'An', role: 'Customer' })),
    http.get(base + '/admin/products', adminApi),
  )
  open('/admin/products')
  expect(await screen.findByRole('heading', { name: 'Bạn không có quyền truy cập' })).toBeVisible()
  expect(adminApi).not.toHaveBeenCalled()
})
it('form sản phẩm validate tên, giá và quan hệ giá giảm', async () => {
  const save = vi.fn(() => ok(product))
  server.use(http.post(base + '/products', save))
  const user = open('/admin/products')
  await user.click(await screen.findByRole('button', { name: 'Thêm sản phẩm' }))
  const button = await screen.findByRole('button', { name: 'Lưu sản phẩm' })
  await waitFor(() => expect(button).toBeEnabled())
  await user.click(button)
  expect(await screen.findByText('Tên sản phẩm là bắt buộc.')).toBeVisible()
  expect(screen.getByText('Giá phải lớn hơn 0.')).toBeVisible()
  await user.type(screen.getByLabelText('Tên sản phẩm'), 'Mới')
  fireEvent.change(screen.getByLabelText('Giá'), { target: { value: '100' } })
  fireEvent.change(screen.getByLabelText('Giá giảm'), { target: { value: '200' } })
  await user.click(button)
  expect(await screen.findByText('Giá giảm phải thấp hơn giá gốc.')).toBeVisible()
  expect(save).not.toHaveBeenCalled()
})
it.each([
  ['file.svg', 'image/svg+xml', 20, 'Chỉ chấp nhận ảnh JPG, PNG hoặc WebP.'],
  ['large.png', 'image/png', 2 * 1024 * 1024 + 1, 'Ảnh phải lớn hơn 0 và không quá 2 MB.'],
])('upload sai bị chặn: %s', async (name, type, size, message) => {
  const upload = vi.fn(() => ok({ imageUrl: '/uploads/a.png' }))
  server.use(http.post(base + '/products/1/image', upload))
  const user = open('/admin/products')
  await user.click(await screen.findByRole('button', { name: 'Sửa Vợt Yonex' }))
  await user.upload(screen.getByLabelText('Ảnh sản phẩm'), new File([new Uint8Array(size)], name, { type }))
  expect(await screen.findByText(message)).toBeVisible()
  expect(screen.getByRole('button', { name: 'Lưu sản phẩm' })).toBeDisabled()
  expect(upload).not.toHaveBeenCalled()
})
it('sửa sản phẩm upload ảnh có preview và gửi multipart', async () => {
  const save = vi.fn()
  const upload = vi.fn()
  const append = vi.spyOn(FormData.prototype, 'append')
  server.use(
    http.put(base + '/products/1', async ({ request }) => { save(await request.json()); return ok(product) }),
    http.post(base + '/products/1/image', async ({ request }) => { const body = await request.text(); upload(body); return ok({ imageUrl: '/uploads/a.png' }) }),
  )
  const user = open('/admin/products')
  await user.click(await screen.findByRole('button', { name: 'Sửa Vợt Yonex' }))
  await user.upload(screen.getByLabelText('Ảnh sản phẩm'), new File(['image'], 'a.png', { type: 'image/png' }))
  expect(await screen.findByAltText('Xem trước ảnh sản phẩm')).toHaveAttribute('src', 'blob:preview')
  await waitFor(() => expect(screen.getByRole('button', { name: 'Lưu sản phẩm' })).toBeEnabled())
  await user.click(screen.getByRole('button', { name: 'Lưu sản phẩm' }))
  await waitFor(() => expect(upload).toHaveBeenCalledExactlyOnceWith(expect.stringContaining('name="file"')))
  // MSW/jsdom chuyển File thành blob khi dựng Request; kiểm tra tên và nội dung trước ranh giới đó.
  expect(append).toHaveBeenCalledWith('file', expect.objectContaining({ name: 'a.png', size: 5, type: 'image/png' }), 'a.png')
  append.mockRestore()
  expect(save).toHaveBeenCalledWith(expect.objectContaining({ name: product.name, price: product.price }))
  await waitFor(() => expect(screen.queryByRole('button', { name: 'Lưu sản phẩm' })).not.toBeInTheDocument())
})
it('ẩn rồi hiện sản phẩm dùng API admin có cả sản phẩm ẩn', async () => {
  let active = true
  server.use(
    http.get(base + '/admin/products', () => ok({ items: [{ ...product, isActive: active }], totalPages: 1 })),
    http.put(base + '/products/1', async ({ request }) => { active = (await request.json() as { isActive: boolean }).isActive; return ok({ ...product, isActive: active }) }),
  )
  const user = open('/admin/products')
  await user.click(await screen.findByRole('button', { name: 'Ẩn Vợt Yonex' }))
  await user.click(await screen.findByRole('button', { name: 'Hiện Vợt Yonex' }))
  expect(await screen.findByRole('button', { name: 'Ẩn Vợt Yonex' })).toBeVisible()
})
it('bảng đơn hàng chỉ hiện chuyển đổi hợp lệ, cập nhật và lọc', async () => {
  const requests = vi.fn()
  let status = 'Pending'
  server.use(
    http.get(base + '/admin/orders', ({ request }) => { requests(new URL(request.url).searchParams.get('status')); return ok({ items: [
      { id: 1, code: 'BS1', status, totalAmount: 100, paymentMethod: 'COD', paymentStatus: 'Unpaid' },
      { id: 2, code: 'BS2', status: 'Shipping', totalAmount: 200, paymentMethod: 'VNPAY', paymentStatus: 'Paid' },
      { id: 3, code: 'BS3', status: 'Completed', totalAmount: 300, paymentStatus: 'Paid' },
    ], totalPages: 1 }) }),
    http.put(base + '/admin/orders/1/status', async ({ request }) => { status = (await request.json() as { status: string }).status; return ok({}) }),
  )
  const user = open('/admin/orders')
  const select = await screen.findByLabelText('Trạng thái mới BS1')
  expect(within(select).getAllByRole('option').map(x => x.textContent)).toEqual(['Chọn bước tiếp', 'Confirmed', 'Cancelled'])
  expect(within(screen.getByLabelText('Trạng thái mới BS2')).getAllByRole('option').map(x => x.textContent)).toEqual(['Chọn bước tiếp', 'Completed'])
  expect(screen.queryByLabelText('Trạng thái mới BS3')).not.toBeInTheDocument()
  await user.selectOptions(select, 'Confirmed')
  await user.click(within(select.closest('tr')!).getByRole('button', { name: 'Cập nhật' }))
  await waitFor(() => expect(within(screen.getByLabelText('Trạng thái mới BS1')).getByRole('option', { name: 'Shipping' })).toBeInTheDocument())
  await user.selectOptions(screen.getByLabelText('Lọc trạng thái'), 'Completed')
  await waitFor(() => expect(requests).toHaveBeenCalledWith('Completed'))
})
it('admin xóa review sau xác nhận', async () => {
  let removed = false
  const remove = vi.fn(() => { removed = true; return ok(null) })
  server.use(
    http.get(base + '/admin/reviews', () => ok({ items: removed ? [] : [{ id: 1, productId: 1, userId: 2, rating: 1, comment: 'Vi phạm' }], totalPages: 1 })),
    http.delete(base + '/products/1/reviews/1', remove),
  )
  const user = open('/admin/reviews')
  await user.click(await screen.findByRole('button', { name: 'Xóa đánh giá #1' }))
  expect(remove).not.toHaveBeenCalled()
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xác nhận xóa' }))
  expect(await screen.findByText('Chưa có đánh giá.')).toBeVisible()
})
it('admin thêm mã validate rồi lưu, xóa có xác nhận', async () => {
  let items: unknown[] = []
  const payload = vi.fn()
  server.use(
    http.get(base + '/admin/coupons', () => ok(items)),
    http.post(base + '/admin/coupons', async ({ request }) => { const input = await request.json(); payload(input); items = [{ ...(input as object), id: 1, usedCount: 0 }]; return ok(items[0]) }),
    http.delete(base + '/admin/coupons/1', () => { items = []; return ok(null) }),
  )
  const user = open('/admin/coupons')
  await user.click(await screen.findByRole('button', { name: 'Thêm mã' }))
  await user.click(screen.getByRole('button', { name: 'Lưu mã' }))
  expect(await screen.findByText('Nhập mã giảm giá.')).toBeVisible()
  expect(payload).not.toHaveBeenCalled()
  await user.type(screen.getByLabelText('Mã'), 'SALE')
  fireEvent.change(screen.getByLabelText('Bắt đầu'), { target: { value: '2026-01-01T00:00' } })
  fireEvent.change(screen.getByLabelText('Kết thúc'), { target: { value: '2026-12-01T00:00' } })
  await user.click(screen.getByRole('button', { name: 'Lưu mã' }))
  await user.click(await screen.findByRole('button', { name: 'Xóa SALE' }))
  await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Xác nhận xóa' }))
  expect(await screen.findByText('Chưa có mã giảm giá.')).toBeVisible()
  expect(payload).toHaveBeenCalledWith(expect.objectContaining({ code: 'SALE', type: 'Percent', value: 10 }))
})

