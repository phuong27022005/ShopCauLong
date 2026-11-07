import { act, fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { MemoryRouter, useLocation, useNavigate } from 'react-router-dom'
import { afterEach, beforeEach, expect, it, vi } from 'vitest'
import { App } from '../App'
import * as catalogApi from '../catalog/api'
import { server } from './server'

const calls: URLSearchParams[] = []
const ok = (data: unknown) => HttpResponse.json({ success: true, data, message: '' })
function Location() {
  const location = useLocation()
  const navigate = useNavigate()
  return <><output aria-label="URL">{location.search}</output><button onClick={() => navigate(-1)}>Back</button></>
}
function open(path = '/products') {
  render(<MemoryRouter initialEntries={[path]}><App /><Location /></MemoryRouter>)
  return userEvent.setup()
}
beforeEach(() => {
  calls.length = 0
  server.use(
    http.get('http://localhost:5173/api/brands', () => ok([{ id: 1, name: 'Yonex', logo: null }])),
    http.get('http://localhost:5173/api/products', ({ request }) => {
      const params = new URL(request.url).searchParams
      calls.push(params)
      return ok({ items: [], page: Number(params.get('page')), pageSize: 12, totalItems: 0, totalPages: 0 })
    }),
  )
})
afterEach(() => { vi.useRealTimers(); vi.restoreAllMocks() })

it('debounce 400ms chỉ gửi từ khóa cuối', async () => {
  open()
  await screen.findByText('Không có sản phẩm trên trang này.')
  vi.useFakeTimers()
  fireEvent.change(screen.getByLabelText('Tìm sản phẩm'), { target: { value: 'yo' } })
  await act(() => vi.advanceTimersByTimeAsync(200))
  fireEvent.change(screen.getByLabelText('Tìm sản phẩm'), { target: { value: 'yonex' } })
  await act(() => vi.advanceTimersByTimeAsync(399))
  expect(calls).toHaveLength(1)
  await act(() => vi.advanceTimersByTimeAsync(1))
  vi.useRealTimers()
  await waitFor(() => expect(calls).toHaveLength(2))
  expect(calls[1].get('search')).toBe('yonex')
})

it('tick thương hiệu cập nhật URL, request và quay về trang 1; Back khôi phục trang 3', async () => {
  const user = open('/products?page=3')
  await screen.findByText('Không có sản phẩm trên trang này.')
  await user.click(await screen.findByLabelText('Yonex'))
  await waitFor(() => expect(calls).toHaveLength(2))
  expect(screen.getByLabelText('URL')).toHaveTextContent('?brandId=1')
  expect(calls[1].get('page')).toBe('1')
  expect(calls[1].getAll('brandId')).toEqual(['1'])
  await user.click(screen.getByRole('button', { name: 'Back' }))
  await waitFor(() => expect(calls).toHaveLength(3))
  expect(screen.getByLabelText('Yonex')).not.toBeChecked()
  expect(calls[2].get('page')).toBe('3')
})

it('xóa bộ lọc hủy cả từ khóa đang chờ debounce', async () => {
  open()
  await screen.findByText('Không có sản phẩm trên trang này.')
  vi.useFakeTimers()
  fireEvent.change(screen.getByLabelText('Tìm sản phẩm'), { target: { value: 'yonex' } })
  fireEvent.click(screen.getByRole('button', { name: 'Xóa bộ lọc' }))
  await act(() => vi.advanceTimersByTimeAsync(500))
  expect(screen.getByLabelText('Tìm sản phẩm')).toHaveValue('')
  expect(screen.getByLabelText('URL')).toBeEmptyDOMElement()
  expect(calls).toHaveLength(1)
})

it('khôi phục toàn bộ bộ lọc từ URL và xóa từng tag', async () => {
  const user = open('/products?search=vot&brandId=1&minPrice=500000&maxPrice=2000000&weight=4U&stiffness=Cứng&inStock=true&sortBy=price&sortDir=desc')
  expect(await screen.findByLabelText('Yonex')).toBeChecked()
  expect(screen.getByLabelText('Tìm sản phẩm')).toHaveValue('vot')
  expect(screen.getByLabelText('Giá từ')).toHaveValue(500000)
  expect(screen.getByLabelText('Giá đến')).toHaveValue(2000000)
  expect(screen.getByLabelText('4U')).toBeChecked()
  expect(screen.getByLabelText('Độ cứng')).toHaveValue('Cứng')
  expect(screen.getByLabelText('Chỉ hiện hàng còn hàng')).toBeChecked()
  expect(screen.getByLabelText('Sắp xếp')).toHaveValue('price:desc')
  await user.click(screen.getByRole('button', { name: 'Xóa Trọng lượng: 4U' }))
  await waitFor(() => expect(calls.at(-1)?.has('weight')).toBe(false))
  expect(screen.getByLabelText('4U')).not.toBeChecked()
})

it('xóa bộ lọc reset URL, danh sách và hiển thị gợi ý trong EmptyState', async () => {
  const user = open('/products?brandId=1&page=3')
  expect(await screen.findByRole('button', { name: 'Xóa bộ lọc để xem thêm sản phẩm' })).toBeVisible()
  await user.click(screen.getByRole('button', { name: 'Xóa bộ lọc' }))
  await waitFor(() => expect(calls).toHaveLength(2))
  expect(screen.getByLabelText('URL')).toBeEmptyDOMElement()
  expect(calls[1].has('brandId')).toBe(false)
  expect(calls[1].get('page')).toBe('1')
})

it('hủy request đang chờ khi chọn bộ lọc mới', async () => {
  const requestSpy = vi.spyOn(catalogApi, 'getFilteredProducts')
  let started = false
  server.use(http.get('http://localhost:5173/api/products', async ({ request }) => {
    const params = new URL(request.url).searchParams
    if (!params.has('brandId')) {
      started = true
      await delay(100)
    }
    return ok({ items: [], page: 1, pageSize: 12, totalItems: 0 })
  }))
  const user = open()
  await waitFor(() => expect(started).toBe(true))
  await user.click(await screen.findByLabelText('Yonex'))
  await screen.findByText('Không có sản phẩm trên trang này.')
  expect(requestSpy.mock.calls[0][1].aborted).toBe(true)
})

it('áp dụng giá hợp lệ và không gửi khoảng giá đảo ngược', async () => {
  const user = open()
  await screen.findByText('Không có sản phẩm trên trang này.')
  fireEvent.change(screen.getByLabelText('Giá từ'), { target: { value: '200' } })
  fireEvent.change(screen.getByLabelText('Giá đến'), { target: { value: '100' } })
  await user.click(screen.getByRole('button', { name: 'Áp dụng giá' }))
  expect(screen.getByRole('alert')).toHaveTextContent('Giá từ phải nhỏ hơn')
  expect(calls).toHaveLength(1)
  fireEvent.change(screen.getByLabelText('Giá đến'), { target: { value: '300' } })
  await user.click(screen.getByRole('button', { name: 'Áp dụng giá' }))
  await waitFor(() => expect(calls).toHaveLength(2))
  expect(calls[1].get('minPrice')).toBe('200')
  expect(calls[1].get('maxPrice')).toBe('300')
})

