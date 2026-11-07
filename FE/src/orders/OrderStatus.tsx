const statuses: Record<string, { label: string; color: string }> = {
  Pending: { label: 'Chờ xác nhận', color: 'bg-amber-100 text-amber-900' },
  Confirmed: { label: 'Đã xác nhận', color: 'bg-blue-100 text-blue-900' },
  Shipping: { label: 'Đang giao', color: 'bg-violet-100 text-violet-900' },
  Completed: { label: 'Hoàn thành', color: 'bg-green-100 text-green-900' },
  Cancelled: { label: 'Đã hủy', color: 'bg-red-100 text-red-900' },
}
export function OrderStatus({ status }: { status: string }) {
  const value = statuses[status] ?? { label: status, color: 'bg-slate-100 text-slate-800' }
  return <span className={'inline-block rounded-full px-3 py-1 text-sm font-semibold ' + value.color}>{value.label}</span>
}
export function OrderTimeline({ status }: { status: string }) {
  const steps = ['Pending', 'Confirmed', 'Shipping', 'Completed']
  if (status === 'Cancelled') return <p className="border-l-4 border-red-500 pl-3">Đơn hàng đã hủy.</p>
  return <ol aria-label="Tiến trình đơn hàng" className="flex flex-wrap gap-3">
    {steps.map((step, index) => <li key={step} aria-current={step === status ? 'step' : undefined}
      className={index <= steps.indexOf(status) ? 'border-b-2 border-teal-700 pb-2' : 'pb-2 opacity-50'}>
      {statuses[step].label}
    </li>)}
  </ol>
}
