import { useEffect, useState } from 'react'

type Load<T> = (signal: AbortSignal) => Promise<T>
type Result<T> = { status: 'success'; data: T } | { status: 'error'; error: unknown }

export function useCatalogResource<T>(load: Load<T>) {
  const [attempt, setAttempt] = useState(0)
  const [snapshot, setSnapshot] = useState<{ load: Load<T>; attempt: number; result: Result<T> }>()

  useEffect(() => {
    const controller = new AbortController()
    const update = (result: Result<T>) => {
      if (!controller.signal.aborted) setSnapshot({ load, attempt, result })
    }

    void load(controller.signal).then(
      (data) => update({ status: 'success', data }),
      (error: unknown) => update({ status: 'error', error }),
    )

    // Hủy request cũ khi người dùng đổi trang hoặc đổi sản phẩm.
    return () => controller.abort()
  }, [load, attempt])

  const state = snapshot?.load === load && snapshot.attempt === attempt
    ? snapshot.result
    : { status: 'loading' as const }
  return { state, retry: () => setAttempt((value) => value + 1) }
}
