import { fireEvent, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { App } from '../App'
import placeholder from '../assets/racket-placeholder.svg'
import { CartProvider } from '../cart/CartContext'
import { AuthProvider } from '../auth/AuthContext'
import { ProductCard } from '../catalog/components/ProductCard'
import { formatVND } from '../catalog/formatVND'
import type { ProductDetail, ProductSummary } from '../catalog/types'
import { server } from './server'

const apiUrl = (path: string) => `http://localhost:5173/api/${path}`
beforeEach(() => server.use(http.get(apiUrl('brands'), () => success([]))))
const success = <T,>(data: T) => HttpResponse.json({ success: true, data, message: 'Thành công.' })
const product: ProductSummary = {
  id: 1,
  name: 'Yonex Astrox',
  slug: 'yonex-astrox',
  brandName: 'Yonex',
  price: 1_250_000,
  discountPrice: null,
  imageUrl: '/uploads/vot.jpg',
  stock: 2,
}

function open(path: string) {
  const user = userEvent.setup()
  render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>)
  return user
}

function renderCard(item: ProductSummary) {
  render(<MemoryRouter><AuthProvider><CartProvider><ProductCard product={item} /></CartProvider></AuthProvider></MemoryRouter>)
}

describe('Giá và thẻ sản phẩm', () => {
  it('định dạng VND với dấu chấm và khoảng trắng thông thường', () => {
    expect(formatVND(1_250_000)).toBe('1.250.000 ₫')
  })

  it('hiện hãng, giá thường và nút thêm vào giỏ', async () => {
    renderCard(product)

    expect(screen.getByText('Yonex')).toBeVisible()
    expect(screen.getByText('1.250.000 ₫')).toBeVisible()
    expect(screen.queryByRole('deletion')).not.toBeInTheDocument()
    expect(await screen.findByRole('button', { name: 'Thêm vào giỏ' })).toBeEnabled()
  })

  it('hiện giá bán, gạch giá gốc và vô hiệu hóa nút khi hết hàng', () => {
    renderCard({ ...product, discountPrice: 999_000, stock: 0 })

    expect(screen.getByText('999.000 ₫')).toBeVisible()
    expect(screen.getByRole('deletion')).toHaveTextContent('Giá gốc: 1.250.000 ₫')
    expect(screen.getByRole('button', { name: 'Hết hàng' })).toBeDisabled()
  })

  it('dùng lazy loading và thay ảnh lỗi bằng ảnh mặc định', () => {
    renderCard(product)
    const image = screen.getByAltText('')
    expect(image).toHaveAttribute('loading', 'lazy')
    expect(image).toHaveAttribute('src', 'http://localhost:5173/uploads/vot.jpg')

    fireEvent.error(image)

    expect(image).toHaveAttribute('src', placeholder)
  })
})

describe('Danh sách sản phẩm', () => {
  it('hiện skeleton trong lúc tải, sau đó hiển thị sản phẩm', async () => {
    server.use(http.get(apiUrl('products'), async () => {
      await delay(60)
      return success({ items: [product], page: 1, pageSize: 12, totalItems: 1 })
    }))

    open('/products')

    expect(screen.getByRole('status')).toHaveTextContent('Đang tải sản phẩm…')
    expect(await screen.findByRole('heading', { name: product.name })).toBeVisible()
    expect(screen.queryByRole('status')).not.toBeInTheDocument()
  })

  it('hiện trạng thái rỗng', async () => {
    server.use(http.get(apiUrl('products'), () => success({ items: [], page: 1, pageSize: 12, totalItems: 0 })))

    open('/products')

    expect(await screen.findByText('Không có sản phẩm trên trang này.')).toBeVisible()
    expect(screen.queryByRole('navigation', { name: 'Phân trang sản phẩm' })).not.toBeInTheDocument()
  })

  it('hiện lỗi API và cho phép tải lại', async () => {
    let attempts = 0
    server.use(http.get(apiUrl('products'), () => {
      attempts += 1
      if (attempts === 1) return new HttpResponse(null, { status: 500 })
      return success({ items: [product], page: 1, pageSize: 12, totalItems: 1 })
    }))
    const user = open('/products')

    expect(await screen.findByRole('alert')).toHaveTextContent('Không tải được danh sách sản phẩm.')
    await user.click(screen.getByRole('button', { name: 'Thử lại' }))

    expect(await screen.findByRole('heading', { name: product.name })).toBeVisible()
    expect(attempts).toBe(2)
  })

  it('gọi API với page và pageSize đúng khi sang trang sau', async () => {
    const queries = vi.fn()
    server.use(http.get(apiUrl('products'), ({ request }) => {
      const params = new URL(request.url).searchParams
      queries(params.get('page'), params.get('pageSize'))
      const page = Number(params.get('page'))
      const item = page === 1 ? product : { ...product, id: 2, name: 'Victor Auraspeed', slug: 'victor-auraspeed' }
      return success({ items: [item], page, pageSize: 12, totalItems: 13 })
    }))
    const user = open('/products')

    expect(await screen.findByRole('heading', { name: product.name })).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Trang sau' }))

    expect(await screen.findByRole('heading', { name: 'Victor Auraspeed' })).toBeVisible()
    expect(screen.getByText('Trang 2 / 2')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Trang sau' })).toBeDisabled()
    expect(queries.mock.calls).toEqual([['1', '12'], ['2', '12']])
  })
})

describe('Trang chủ và chi tiết sản phẩm', () => {
  it('hiển thị banner, sản phẩm mới và danh sách thương hiệu', async () => {
    const newest = vi.fn()
    server.use(
      http.get(apiUrl('products'), ({ request }) => {
        newest(new URL(request.url).searchParams.get('newestFirst'))
        return success({ items: [product], page: 1, pageSize: 4, totalItems: 1 })
      }),
      http.get(apiUrl('brands'), () => success([{ id: 5, name: 'Victor', logo: null }])),
    )

    open('/')

    expect(screen.getByRole('heading', { name: 'Cửa hàng vợt cầu lông' })).toBeVisible()
    expect(screen.getByRole('link', { name: 'Khám phá sản phẩm' })).toHaveAttribute('href', '/products')
    const latest = screen.getByRole('region', { name: 'Sản phẩm mới' })
    expect(await within(latest).findByRole('heading', { name: product.name })).toBeVisible()
    const brands = screen.getByRole('region', { name: 'Thương hiệu' })
    expect(await within(brands).findByText('Victor')).toBeVisible()
    expect(newest).toHaveBeenCalledExactlyOnceWith('true')
  })

  it('hiện trang 404 nếu slug không tồn tại', async () => {
    server.use(http.get(apiUrl('products/khong-ton-tai'), () =>
      HttpResponse.json({ success: false, data: null, message: 'Sản phẩm không tồn tại.' }, { status: 404 }),
    ))

    open('/products/khong-ton-tai')

    expect(await screen.findByRole('heading', { name: 'Không tìm thấy sản phẩm' })).toBeVisible()
    expect(screen.getByText('404')).toBeVisible()
    expect(screen.getByRole('link', { name: 'Về danh sách sản phẩm' })).toHaveAttribute('href', '/products')
  })

  it('hiện thông số, mô tả, tồn kho và thêm vào giỏ tạm', async () => {
    const detail: ProductDetail = {
      ...product,
      brand: { id: 1, name: 'Yonex', logo: null },
      brandId: 1,
      category: { id: 1, name: 'Vợt tấn công', slug: 'vot-tan-cong' },
      categoryId: 1,
      description: 'Vợt phù hợp cho lối chơi tấn công.',
      weight: '4U',
      stiffness: 'Cứng',
      balancePoint: 'Nặng đầu',
      isActive: true,
      createdAt: '2026-01-01T00:00:00Z',
    }
    server.use(http.get(apiUrl('products/yonex-astrox'), () => success(detail)))
    const user = open('/products/yonex-astrox')

    expect(await screen.findByRole('heading', { name: product.name })).toBeVisible()
    expect(screen.getByRole('img', { name: product.name })).toBeVisible()
    for (const text of ['4U', 'Cứng', 'Nặng đầu', detail.description!, 'Còn 2 sản phẩm']) {
      expect(screen.getByText(text)).toBeVisible()
    }

    const button = screen.getByRole('button', { name: 'Thêm vào giỏ' })
    await user.click(button)
    await user.click(button)

    expect(screen.getByRole('status')).toHaveTextContent('Đã thêm sản phẩm vào giỏ hàng.')
    expect(button).toBeDisabled()
  })
})
