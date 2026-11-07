import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/AuthContext'
import { SessionStatus } from '../auth/AuthRoutes'
import { authErrorMessage } from '../auth/client'
import { loginSchema } from '../auth/schemas'
import type { LoginInput } from '../auth/schemas'
import { AuthCard, AuthSubmit } from '../components/AuthCard'
import { AuthField } from '../components/AuthField'

export function LoginPage() {
  const auth = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const { register, handleSubmit, setError, clearErrors, formState } = useForm<LoginInput>({
    resolver: zodResolver(loginSchema),
  })
  const { errors, isSubmitting } = formState

  async function submit(input: LoginInput) {
    try {
      await auth.login(input)
      const returnTo: unknown = location.state?.returnTo
      const path = typeof returnTo === 'string' && /^\/(?![\/\\])/.test(returnTo) ? returnTo : '/account'
      void navigate(path, { replace: true })
    } catch (error) {
      setError('root', { message: authErrorMessage(error, 'Không thể đăng nhập. Vui lòng thử lại.') })
    }
  }

  return (
    <AuthCard title="Đăng nhập">
      {auth.status !== 'ready' ? <SessionStatus /> : (
        <>
          {location.state?.registered && <p role="status" className="mb-4 text-teal-800">Đăng ký thành công. Bạn có thể đăng nhập.</p>}
          <form noValidate className="space-y-4" onSubmit={(event) => {
            clearErrors('root')
            void handleSubmit(submit)(event)
          }}>
            <AuthField label="Email" type="email" autoComplete="email" field={register('email')} error={errors.email?.message} />
            <AuthField label="Mật khẩu" type="password" autoComplete="current-password" field={register('password')} error={errors.password?.message} />
            {errors.root?.message && <p role="alert" className="text-sm text-red-800">{errors.root.message}</p>}
            <AuthSubmit busy={isSubmitting} label="Đăng nhập" />
          </form>
          <p className="mt-4 text-sm">Chưa có tài khoản? <Link to="/register" className="underline">Đăng ký</Link></p>
        </>
      )}
    </AuthCard>
  )
}
