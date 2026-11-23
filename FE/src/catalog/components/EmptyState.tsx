import { ShopIllustration } from '../../components/ShopIllustration'
export function EmptyState({ message }: { message: string }) {
  return <div className="rounded-xl border border-dashed border-slate-300 bg-white p-6 text-center text-slate-700"><ShopIllustration /><p>{message}</p></div>
}
