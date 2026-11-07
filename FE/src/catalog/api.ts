import axios from 'axios'
import type { ApiResponse } from '../auth/types'
import type { Brand, ProductDetail, ProductPage } from './types'

const baseURL = import.meta.env.VITE_API_BASE_URL || '/api'
const api = axios.create({ baseURL })

export async function getFilteredProducts(params: URLSearchParams, signal: AbortSignal) {
  const { data } = await api.get<ApiResponse<ProductPage>>('/products', { params, signal })
  return unwrap(data)
}

function unwrap<T>(response: ApiResponse<T>): T {
  if (!response.success) throw new Error(response.message)
  return response.data
}

export async function getProducts(page: number, pageSize: number, signal: AbortSignal, newestFirst = false) {
  const { data } = await api.get<ApiResponse<ProductPage>>('/products', {
    signal,
    params: { page, pageSize, ...(newestFirst ? { newestFirst: true } : {}) },
  })
  return unwrap(data)
}

export async function getProduct(slug: string, signal: AbortSignal) {
  const { data } = await api.get<ApiResponse<ProductDetail>>(`/products/${encodeURIComponent(slug)}`, { signal })
  return unwrap(data)
}

export async function getBrands(signal: AbortSignal) {
  const { data } = await api.get<ApiResponse<Brand[]>>('/brands', { signal })
  return unwrap(data)
}

export function isProductNotFound(error: unknown) {
  return axios.isAxiosError(error) && error.response?.status === 404
}

export function catalogImageUrl(url: string) {
  // Đường dẫn upload của BE nằm ngoài tiền tố /api.
  return url.startsWith('/uploads/') ? new URL(url, new URL(baseURL, window.location.origin)).href : url
}
