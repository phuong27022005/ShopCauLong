export interface Brand {
  id: number
  name: string
  logo: string | null
}

export interface ProductSummary {
  id: number
  name: string
  slug: string
  price: number
  discountPrice: number | null
  imageUrl: string | null
  brandName: string
  stock: number
}

export interface ProductDetail extends Omit<ProductSummary, 'brandName'> {
  description: string | null
  brandId: number
  categoryId: number
  brand: Brand
  category: { id: number; name: string; slug: string }
  weight: string
  stiffness: string
  balancePoint: string
  isActive: boolean
  createdAt: string
}

export interface ProductPage {
  items: ProductSummary[]
  page: number
  pageSize: number
  totalItems: number
}
