export function ResourceState({ loading, error, reload }: { loading: boolean; error: string; reload: () => void }) {
  if (loading) return <p role="status">Đang tải dữ liệu…</p>
  if (error) return <div role="alert"><p>{error}</p><button onClick={reload} className="underline">Thử lại</button></div>
  return null
}

