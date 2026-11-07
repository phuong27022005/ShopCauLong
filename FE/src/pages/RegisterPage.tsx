import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { authErrorMessage } from '../auth/client'
import { registerSchema } from '../auth/schemas'
import type { RegisterInput } from '../auth/schemas'
import { AuthCard, AuthSubmit } from '../components/AuthCard'
import { AuthField } from '../components/AuthField'

export function RegisterPage() {
  const auth = useAuth()
  const navigate = useNavigate()
  const { register, handleSubmit, setError, clearErrors, formState } = useForm<RegisterInput>({
    resolver: zodResolver(registerSchema),
  })
  const { errors, isSubmitting } = formState

  async function submit(input: RegisterInput) {
    try {
      await auth.register(input)
      void navigate('/login', { replace: true, state: { registered: true } })
    } catch (error) {
      setError('root', { message: authErrorMessage(error, 'Không thể đăng ký. Vui lòng thử lại.') })
    }
  }

  return (
    <AuthCard title="Đăng ký">
      <form noValidate className="space-y-4" onSubmit={(event) => {
        clearErrors('root')
        void handleSubmit(submit)(event)
      }}>
        <AuthField label="Họ và tên" autoComplete="name" field={register('fullName')} error={errors.fullName?.message} />
        <AuthField label="Email" type="email" autoComplete="email" field={register('email')} error={errors.email?.message} />
        <AuthField label="Mật khẩu" type="password" autoComplete="new-password" field={register('password')} error={errors.password?.message} />
        <AuthField label="Xác nhận mật khẩu" type="password" autoComplete="new-password"
          field={register('confirmPassword')} error={errors.confirmPassword?.message} />
        {errors.root?.message && <p role="alert" className="text-sm text-red-800">{errors.root.message}</p>}
        <AuthSubmit busy={isSubmitting} label="Đăng ký" />
      </form>
      <p className="mt-4 text-sm">Đã có tài khoản? <Link to="/login" className="underline">Đăng nhập</Link></p>
    </AuthCard>
  )
}
