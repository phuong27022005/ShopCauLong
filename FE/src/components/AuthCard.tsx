import type { ReactNode } from 'react'

export function AuthCard({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="mx-auto w-full max-w-md rounded-xl border border-slate-200 bg-white p-5 shadow-sm sm:p-8">
      <h1 className="mb-5 text-2xl font-bold">{title}</h1>
      {children}
    </section>
  )
}

export function AuthSubmit({ busy, label }: { busy: boolean; label: string }) {
  return (
    <button type="submit" disabled={busy}
      className="w-full rounded-md bg-teal-800 px-4 py-2 font-semibold text-white hover:bg-teal-900 disabled:opacity-60">
      {busy ? 'Đang gửi…' : label}
    </button>
  )
}
