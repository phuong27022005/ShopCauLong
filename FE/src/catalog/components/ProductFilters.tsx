import { useState } from 'react'
import type { Brand } from '../types'

export type ChangeFilter = (key: string, values: string[]) => void

export function ProductFilters({ params, brands, onChange }: {
  params: URLSearchParams; brands: Brand[]; onChange: ChangeFilter
}) {
  const [min, setMin] = useState(params.get('minPrice') ?? '')
  const [max, setMax] = useState(params.get('maxPrice') ?? '')
  const [error, setError] = useState('')
  function toggle(key: string, value: string, checked: boolean) {
    const current = params.getAll(key)
    onChange(key, checked ? [...current, value] : current.filter((item) => item !== value))
  }

  return <div className="space-y-6">
    <fieldset className="space-y-2">
      <legend className="mb-2 font-semibold">Thương hiệu</legend>
      {brands.map((brand) => <label key={brand.id} className="flex items-center gap-2">
        <input type="checkbox" checked={params.getAll('brandId').includes(String(brand.id))}
          onChange={(event) => toggle('brandId', String(brand.id), event.target.checked)} />
        {brand.name}
      </label>)}
    </fieldset>
    <form className="space-y-2" onSubmit={(event) => {
      event.preventDefault()
      if ((min && Number(min) < 0) || (max && Number(max) < 0) || (min && max && Number(min) > Number(max))) {
        setError('Giá từ phải nhỏ hơn hoặc bằng giá đến, và không được âm.')
        return
      }
      setError('')
      onChange('priceRange', [min, max])
    }}>
      <p className="font-semibold">Khoảng giá (VND)</p>
      <label className="block">Giá từ<input type="number" min="0" step="1" value={min}
        onChange={(event) => setMin(event.target.value)} className="w-full rounded border bg-white p-2" /></label>
      <label className="block">Giá đến<input type="number" min="0" step="1" value={max}
        onChange={(event) => setMax(event.target.value)} className="w-full rounded border bg-white p-2" /></label>
      {error && <p role="alert" className="text-sm text-red-800">{error}</p>}
      <button className="rounded border px-3 py-2" type="submit">Áp dụng giá</button>
    </form>
    <fieldset className="space-y-2">
      <legend className="mb-2 font-semibold">Trọng lượng</legend>
      {['2U', '3U', '4U', '5U', 'F'].map((weight) => <label key={weight} className="flex items-center gap-2">
        <input type="checkbox" checked={params.getAll('weight').includes(weight)}
          onChange={(event) => toggle('weight', weight, event.target.checked)} />{weight}
      </label>)}
    </fieldset>
    <label className="block">Độ cứng
      <select value={params.get('stiffness') ?? ''} onChange={(event) => onChange('stiffness', [event.target.value])}
        className="mt-1 w-full rounded border bg-white p-2">
        <option value="">Tất cả</option>
        {[...new Set(['Mềm', 'Trung bình', 'Cứng', params.get('stiffness')].filter(Boolean))].map((value) =>
          <option key={value} value={value!}>{value}</option>)}
      </select>
    </label>
    <label className="flex items-center gap-2"><input type="checkbox" checked={params.get('inStock') === 'true'}
      onChange={(event) => onChange('inStock', event.target.checked ? ['true'] : [])} />Chỉ hiện hàng còn hàng</label>
  </div>
}
