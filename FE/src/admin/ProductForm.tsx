import { useEffect, useRef, useState } from 'react'
import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { z } from 'zod'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import type { ApiResponse } from '../auth/types'
import type { ProductDetail } from '../catalog/types'
import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'
import { ProductGalleryEditor } from './ProductGalleryEditor'

const schema = z.object({
  skillLevel: z.string().max(100),
  name: z.string().trim().min(1, 'Tên sản phẩm là bắt buộc.').max(200),
  description: z.string().max(4000),
  price: z.number().positive('Giá phải lớn hơn 0.'),
  discountPrice: z.number().nonnegative().nullable(),
  stock: z.number().int().nonnegative('Tồn kho không được âm.'),
  brandId: z.number().int().positive('Chọn thương hiệu.'),
  categoryId: z.number().int().positive('Chọn danh mục.'),
  weight: z.enum(['2U', '3U', '4U', '5U', 'F']),
  stiffness: z.string().max(50), balancePoint: z.string().max(50), isActive: z.boolean(),
}).refine(x => x.discountPrice === null || x.discountPrice < x.price, { path: ['discountPrice'], message: 'Giá giảm phải thấp hơn giá gốc.' })
type Input = z.infer<typeof schema>
export function ProductForm({ product, saved, close }: { product?: ProductDetail; saved: () => void; close: () => void }) {
  const { api } = useAuth()
  const brands = useApiResource<{ id: number; name: string }[]>('/brands')
  const categories = useApiResource<{ id: number; name: string }[]>('/categories')
  const id = useRef(product?.id)
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState('')
  const [fileError, setFileError] = useState('')
  const [error, setError] = useState('')
  const { register, handleSubmit, formState: { errors, isSubmitting } } = useForm<Input>({
    resolver: zodResolver(schema), defaultValues: {
      skillLevel: product?.skillLevel ?? '',
      name: product?.name ?? '', description: product?.description ?? '', price: product?.price ?? 0,
      discountPrice: product?.discountPrice ?? null, stock: product?.stock ?? 0, brandId: product?.brandId ?? 0,
      categoryId: product?.categoryId ?? 0, weight: (product?.weight ?? '4U') as Input['weight'],
      stiffness: product?.stiffness ?? '', balancePoint: product?.balancePoint ?? '', isActive: product?.isActive ?? true,
    },
  })
  useEffect(() => {
    if (!file) { setPreview(''); return }
    const url = URL.createObjectURL(file)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [file])
  function selectFile(next?: File) {
    setFile(null); setFileError('')
    if (!next) return
    if (!['image/jpeg', 'image/png', 'image/webp'].includes(next.type) || !/\.(jpe?g|png|webp)$/i.test(next.name)) {
      setFileError('Chỉ chấp nhận ảnh JPG, PNG hoặc WebP.'); return
    }
    if (!next.size || next.size > 2 * 1024 * 1024) { setFileError('Ảnh phải lớn hơn 0 và không quá 2 MB.'); return }
    setFile(next)
  }
  async function submit(input: Input) {
    if (fileError) return
    setError('')
    try {
      const response = await api.request<ApiResponse<ProductDetail>>({ url: '/products' + (id.current ? '/' + id.current : ''),
        method: id.current ? 'PUT' : 'POST', data: input })
      if (!response.data.success) throw new Error(response.data.message)
      // Giữ ID nếu upload lỗi để lần thử lại không tạo sản phẩm trùng.
      id.current = response.data.data.id
      if (file) {
        const body = new FormData(); body.append('file', file, file.name)
        const upload = await api.post<ApiResponse<{ imageUrl: string }>>('/products/' + id.current + '/image', body)
        if (!upload.data.success) throw new Error(upload.data.message)
      }
      saved()
    } catch (cause) { setError(authErrorMessage(cause, 'Không lưu được sản phẩm.')) }
  }
  return <form noValidate onSubmit={handleSubmit(submit)} className="space-y-3 rounded-xl border bg-white p-4">
    <h3 className="text-xl font-bold">{product ? 'Sửa sản phẩm' : 'Thêm sản phẩm'}</h3>
    <ResourceState {...brands} /><ResourceState {...categories} />
    <div className="grid gap-3 sm:grid-cols-2">
      <label>Tên sản phẩm<input {...register('name')} className="field" /></label>
      <label>Giá<input type="number" step="0.01" {...register('price', { valueAsNumber: true })} className="field" /></label>
      <label>Giá giảm<input type="number" step="0.01" {...register('discountPrice', { setValueAs: value => value === '' ? null : Number(value) })} className="field" /></label>
      <label>Tồn kho<input type="number" {...register('stock', { valueAsNumber: true })} className="field" /></label>
      <label>Thương hiệu<select {...register('brandId', { valueAsNumber: true })} className="field"><option value={0}>Chọn thương hiệu</option>{brands.data?.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label>Danh mục<select {...register('categoryId', { valueAsNumber: true })} className="field"><option value={0}>Chọn danh mục</option>{categories.data?.map(x => <option key={x.id} value={x.id}>{x.name}</option>)}</select></label>
      <label>Trọng lượng<select {...register('weight')} className="field">{['2U', '3U', '4U', '5U', 'F'].map(x => <option key={x}>{x}</option>)}</select></label>
      <label>Độ cứng<input {...register('stiffness')} className="field" /></label>
      <label>Điểm cân bằng<input {...register('balancePoint')} className="field" /></label>
      <label>Trình độ phù hợp<select {...register('skillLevel')} className="field"><option value="">Chưa cập nhật</option>{['Người mới', 'Trung cấp', 'Nâng cao', 'Mọi trình độ'].map(x => <option key={x}>{x}</option>)}</select></label>
    </div>
    <label className="block">Mô tả<textarea {...register('description')} className="field" /></label>
    <label className="block"><input type="checkbox" {...register('isActive')} /> Đang bán</label>
    <label className="block">Ảnh sản phẩm<input type="file" accept=".jpg,.jpeg,.png,.webp" onChange={e => selectFile(e.target.files?.[0])} className="field" /></label>
    {(preview || product?.imageUrl) && <img src={preview || product?.imageUrl || ''} alt="Xem trước ảnh sản phẩm" className="h-40 object-contain" />}
    {product && <ProductGalleryEditor id={product.id} />}
    {!product && <p className="text-sm text-slate-600">Sau khi tạo sản phẩm, mở Sửa để quản lý thư viện nhiều ảnh.</p>}
    {Object.entries(errors).map(([key, value]) => <p key={key} role="alert">{value.message}</p>)}
    {fileError && <p role="alert">{fileError}</p>}{error && <p role="alert">{error}</p>}
    <button disabled={isSubmitting || Boolean(fileError) || !brands.data || !categories.data} className="rounded bg-teal-800 px-4 py-2 text-white">{isSubmitting ? 'Đang lưu…' : 'Lưu sản phẩm'}</button>
    <button type="button" disabled={isSubmitting} onClick={close} className="ml-3 underline">Đóng</button>
  </form>
}

