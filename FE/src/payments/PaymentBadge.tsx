const labels: Record<string, [string, string]> = {
  Unpaid: ['Chưa thanh toán', 'bg-amber-100 text-amber-900'],
  Paid: ['Đã thanh toán', 'bg-green-100 text-green-900'],
  Failed: ['Thanh toán thất bại', 'bg-red-100 text-red-900'],
  Refunded: ['Đã hoàn tiền', 'bg-blue-100 text-blue-900'],
}
export function PaymentBadge({ status }: { status: string | undefined }) {
  const [label, color] = labels[status ?? 'Unpaid'] ?? ['Chưa xác định', 'bg-slate-100 text-slate-900']
  return <span className={'inline-block rounded-full px-3 py-1 text-sm font-semibold ' + color}>{label}</span>
}
