import { z } from 'zod'

const email = z.string().trim()
  .min(1, 'Vui lòng nhập email.')
  .max(254, 'Email tối đa 254 ký tự.')
  .pipe(z.email('Email không hợp lệ.'))

export const loginSchema = z.object({
  email,
  password: z.string().min(1, 'Vui lòng nhập mật khẩu.'),
})

export const registerSchema = loginSchema.extend({
  fullName: z.string().trim()
    .min(1, 'Vui lòng nhập họ tên.')
    .max(150, 'Họ tên tối đa 150 ký tự.'),
  password: z.string()
    .min(8, 'Mật khẩu phải có ít nhất 8 ký tự.')
    .max(128, 'Mật khẩu tối đa 128 ký tự.')
    .regex(/[a-z]/, 'Mật khẩu phải có chữ thường.')
    .regex(/[A-Z]/, 'Mật khẩu phải có chữ hoa.')
    .regex(/[0-9]/, 'Mật khẩu phải có chữ số.')
    .regex(/[^a-zA-Z0-9\s]/, 'Mật khẩu phải có ký tự đặc biệt.'),
  confirmPassword: z.string().min(1, 'Vui lòng xác nhận mật khẩu.'),
}).refine(({ password, confirmPassword }) => password === confirmPassword, {
  path: ['confirmPassword'],
  message: 'Mật khẩu xác nhận không khớp.',
})

export type LoginInput = z.infer<typeof loginSchema>
export type RegisterInput = z.infer<typeof registerSchema>
