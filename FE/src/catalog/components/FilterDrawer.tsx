import { useEffect, useRef } from 'react'
import type { ReactNode } from 'react'

export function FilterDrawer({ onClose, children }: { onClose: () => void; children: ReactNode }) {
  const ref = useRef<HTMLDialogElement>(null)
  useEffect(() => {
    const dialog = ref.current!
    dialog.showModal()
    const previous = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    return () => {
      dialog.close()
      document.body.style.overflow = previous
    }
  }, [])
  return <dialog ref={ref} aria-label="Bộ lọc sản phẩm" onCancel={onClose}
    onClick={(event) => { if (event.target === event.currentTarget) onClose() }}
    className="fixed inset-y-0 left-auto right-0 m-0 h-dvh max-h-none w-80 max-w-full border-0 bg-white p-0 backdrop:bg-black/40">
    <div className="min-h-full p-5">
      <div className="mb-6 flex items-center justify-between">
        <h2 className="text-lg font-bold">Bộ lọc</h2>
        <button type="button" onClick={onClose} className="rounded border px-3 py-2">Đóng bộ lọc</button>
      </div>
      {children}
    </div>
  </dialog>
}
