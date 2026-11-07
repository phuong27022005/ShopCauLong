import { formatVND } from '../formatVND'

interface PriceTagProps {
  price: number
  discountPrice: number | null
}

export function PriceTag({ price, discountPrice }: PriceTagProps) {
  const onSale = discountPrice !== null && discountPrice < price

  return (
    <p className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
      <span className="text-lg font-bold text-teal-900">{formatVND(onSale ? discountPrice : price)}</span>
      {onSale && (
        <del className="text-sm text-slate-600">
          <span className="sr-only">Giá gốc: </span>{formatVND(price)}
        </del>
      )}
    </p>
  )
}
