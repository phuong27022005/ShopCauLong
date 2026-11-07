export function ErrorState({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div className="rounded-xl border border-red-200 bg-red-50 p-5">
      <p role="alert" className="text-red-900">{message}</p>
      <button type="button" onClick={onRetry} className="mt-2 font-semibold text-red-900 underline">Thử lại</button>
    </div>
  )
}
