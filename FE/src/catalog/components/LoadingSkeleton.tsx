export function LoadingSkeleton({ message, count = 4 }: { message: string; count?: number }) {
  return (
    <div role="status">
      <span className="sr-only">{message}</span>
      <div aria-hidden="true" className={count === 1 ? '' : 'grid grid-cols-1 gap-4 min-[420px]:grid-cols-2 lg:grid-cols-4'}>
        {Array.from({ length: count }, (_, index) => (
          <div key={index} className="space-y-3 rounded-xl border border-slate-200 bg-white p-4 motion-safe:animate-pulse">
            <div className="h-32 rounded-lg bg-slate-200" />
            <div className="h-4 w-3/4 rounded bg-slate-200" />
            <div className="h-4 w-1/2 rounded bg-slate-200" />
          </div>
        ))}
      </div>
    </div>
  )
}
