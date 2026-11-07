import { useEffect, useState } from 'react'

export function SearchInput({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const [draft, setDraft] = useState(value)
  useEffect(() => {
    if (draft === value) return
    const timeout = window.setTimeout(() => onChange(draft.trim()), 400)
    return () => window.clearTimeout(timeout)
  }, [draft, value, onChange])

  return <label className="block grow">Tìm sản phẩm
    <input type="search" value={draft} onChange={(event) => setDraft(event.target.value)}
      className="mt-1 w-full rounded-lg border border-slate-300 bg-white px-3 py-2" placeholder="Nhập tên vợt…" />
  </label>
}
