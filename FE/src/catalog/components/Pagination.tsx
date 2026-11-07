interface PaginationProps {
  page: number
  totalPages: number
  onChange: (page: number) => void
  label?: string
}

export function Pagination({ page, totalPages, onChange, label = 'Phân trang sản phẩm' }: PaginationProps) {
  if (totalPages < 2 && page <= 1) return null

  const control = 'rounded-md border border-slate-300 bg-white px-3 py-2 text-sm font-medium disabled:opacity-45'
  return (
    <nav aria-label={label} className="flex flex-wrap items-center justify-center gap-3">
      <button type="button" className={control} disabled={page <= 1} onClick={() => onChange(Math.min(page - 1, totalPages))}>
        Trang trước
      </button>
      <span aria-current="page" className="text-sm text-slate-700">Trang {page} / {totalPages}</span>
      <button type="button" className={control} disabled={page >= totalPages} onClick={() => onChange(page + 1)}>
        Trang sau
      </button>
    </nav>
  )
}
