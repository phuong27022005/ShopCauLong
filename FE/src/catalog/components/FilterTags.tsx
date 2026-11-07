import type { Brand } from '../types'
import type { ChangeFilter } from './ProductFilters'

const labels: Record<string, string> = {
  search: 'Tìm kiếm', brandId: 'Thương hiệu', categoryId: 'Danh mục', minPrice: 'Giá từ',
  maxPrice: 'Giá đến', weight: 'Trọng lượng', stiffness: 'Độ cứng',
  balancePoint: 'Điểm cân bằng', inStock: 'Còn hàng',
}

export function FilterTags({ params, brands, onChange }: {
  params: URLSearchParams; brands: Brand[]; onChange: ChangeFilter
}) {
  return <ul aria-label="Bộ lọc đang áp dụng" className="flex flex-wrap gap-2">
    {[...params.entries()].filter(([key]) => key in labels).map(([key, value], index) => {
      const display = key === 'brandId' ? brands.find((brand) => String(brand.id) === value)?.name ?? value
        : key === 'inStock' ? (value === 'true' ? 'Có' : 'Không') : value
      const label = labels[key] + ': ' + display
      return <li key={key + value + index}><button type="button" aria-label={'Xóa ' + label}
        onClick={() => onChange(key, params.getAll(key).filter((item) => item !== value))}
        className="rounded-full border border-teal-200 bg-teal-50 px-3 py-1 text-sm text-teal-900">
        {label} <span aria-hidden="true">×</span>
      </button></li>
    })}
  </ul>
}
