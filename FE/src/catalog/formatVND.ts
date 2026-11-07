const vnd = new Intl.NumberFormat('vi-VN', {
  style: 'currency',
  currency: 'VND',
  maximumFractionDigits: 0,
})

export function formatVND(amount: number): string {
  return vnd.format(amount).replace(/\u00a0/g, ' ')
}
