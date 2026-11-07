import { useId } from 'react'
import type { HTMLInputTypeAttribute } from 'react'
import type { UseFormRegisterReturn } from 'react-hook-form'

interface AuthFieldProps {
  label: string
  type?: HTMLInputTypeAttribute
  autoComplete: string
  field: UseFormRegisterReturn
  error?: string
}

export function AuthField({ label, type = 'text', autoComplete, field, error }: AuthFieldProps) {
  const id = useId()

  return (
    <div className="space-y-1">
      <label htmlFor={id} className="block text-sm font-medium">{label}</label>
      <input
        {...field}
        id={id}
        type={type}
        autoComplete={autoComplete}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `${id}-error` : undefined}
        className="w-full rounded-md border border-slate-400 px-3 py-2 focus:border-teal-700 focus:outline-2 focus:outline-teal-700 aria-invalid:border-red-700"
      />
      {error && <p id={`${id}-error`} role="alert" className="text-sm text-red-800">{error}</p>}
    </div>
  )
}
