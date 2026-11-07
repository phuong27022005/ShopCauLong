import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { delay, http, HttpResponse } from 'msw'
import { MemoryRouter } from 'react-router-dom'
import { describe, expect, it, vi } from 'vitest'
import { App } from '../App'
import { createAuthClient, TOKEN_KEY } from '../auth/client'
import type { User } from '../auth/types'
import { authUrl, server } from './server'

const customer: User = { id: 2, fullName: 'Khách hàng mẫu', email: 'customer@example.com', role: 'Customer' }
const password = 'MatKhau!123'
const success = <T,>(data: T) => HttpResponse.json({ success: true, data, message: '' })
const unauthorized = (message = 'Phiên đã hết hạn.') =>
  HttpResponse.json({ success: false, data: null, message }, { status: 401 })

function open(path: string) {
  const user = userEvent.setup()
  render(<MemoryRouter initialEntries={[path]}><App /></MemoryRouter>)
  return user
}

async function fillLogin(user: ReturnType<typeof userEvent.setup>) {
  await user.type(screen.getByLabelText('Email'), customer.email)
  await user.type(screen.getByLabelText('Mật khẩu'), password)
  await user.click(screen.getByRole('button', { name: 'Đăng nhập' }))
}

async function fillRegistration(user: ReturnType<typeof userEvent.setup>, value: string) {
  await user.type(screen.getByLabelText('Họ và tên'), customer.fullName)
  await user.type(screen.getByLabelText('Email'), customer.email)
  await user.type(screen.getByLabelText('Mật khẩu', { exact: true }), value)
  await user.type(screen.getByLabelText('Xác nhận mật khẩu'), value)
  await user.click(screen.getByRole('button', { name: 'Đăng ký' }))
}

describe('Biểu mẫu xác thực', () => {
  it.each(['/login', '/register'])('hiện lỗi khi gửi form trống tại %s', async (path) => {
    const request = vi.fn(() => success(null))
    server.use(http.post(authUrl('login'), request), http.post(authUrl('register'), request))
    const user = open(path)

    await user.click(screen.getByRole('button', { name: path === '/login' ? 'Đăng nhập' : 'Đăng ký' }))

    expect(await screen.findByText('Vui lòng nhập email.')).toBeVisible()
    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
    expect(request).not.toHaveBeenCalled()
  })

  it('từ chối email sai định dạng khi đăng nhập', async () => {
    const request = vi.fn(() => success(null))
    server.use(http.post(authUrl('login'), request))
    const user = open('/login')

    await user.type(screen.getByLabelText('Email'), 'email-sai')
    await user.type(screen.getByLabelText('Mật khẩu'), password)
    await user.click(screen.getByRole('button', { name: 'Đăng nhập' }))

    expect(await screen.findByText('Email không hợp lệ.')).toBeVisible()
    expect(request).not.toHaveBeenCalled()
  })

  it('từ chối mật khẩu yếu khi đăng ký', async () => {
    const request = vi.fn(() => success(null))
    server.use(http.post(authUrl('register'), request))
    const user = open('/register')

    await fillRegistration(user, 'abc')

    expect(await screen.findByText('Mật khẩu phải có ít nhất 8 ký tự.')).toBeVisible()
    expect(request).not.toHaveBeenCalled()
  })

  it('gửi dữ liệu đăng ký và chuyển sang trang đăng nhập', async () => {
    const submitted = vi.fn()
    server.use(http.post(authUrl('register'), async ({ request }) => {
      submitted(await request.json())
      return HttpResponse.json({ success: true, data: null, message: '' }, { status: 201 })
    }))
    const user = open('/register')

    await fillRegistration(user, password)

    expect(await screen.findByRole('heading', { name: 'Đăng nhập' })).toBeVisible()
    expect(screen.getByRole('status')).toHaveTextContent('Đăng ký thành công.')
    expect(submitted).toHaveBeenCalledExactlyOnceWith({ fullName: customer.fullName, email: customer.email, password })
  })

  it('hiển thị lỗi đăng ký từ API', async () => {
    server.use(http.post(authUrl('register'), () =>
      HttpResponse.json({ success: false, data: null, message: 'Email đã được sử dụng.' }, { status: 400 }),
    ))

    await fillRegistration(open('/register'), password)

    expect(await screen.findByRole('alert')).toHaveTextContent('Email đã được sử dụng.')
  })

  it('lưu token và về trang yêu cầu đăng nhập, sau đó đăng xuất được', async () => {
    const submitted = vi.fn()
    const logout = vi.fn(() => success(null))
    server.use(
      http.post(authUrl('login'), async ({ request }) => {
        submitted(await request.json())
        return success({ user: customer, accessToken: 'token-login' })
      }),
      http.post(authUrl('logout'), logout),
    )
    const user = open('/account')
    expect(await screen.findByRole('heading', { name: 'Đăng nhập' })).toBeVisible()

    await fillLogin(user)

    expect(await screen.findByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible()
    expect(screen.getByText(customer.fullName)).toBeVisible()
    expect(localStorage.getItem(TOKEN_KEY)).toBe('token-login')
    expect(submitted).toHaveBeenCalledExactlyOnceWith({ email: customer.email, password })

    await user.click(screen.getByRole('button', { name: 'Đăng xuất' }))

    expect(await screen.findByRole('heading', { name: 'Đăng nhập' })).toBeVisible()
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull()
    await vi.waitFor(() => expect(logout).toHaveBeenCalledTimes(1))
  })

  it('hiển thị lỗi đăng nhập từ API và không refresh khi sai thông tin', async () => {
    const refresh = vi.fn(() => unauthorized())
    server.use(
      http.post(authUrl('login'), () => unauthorized('Email hoặc mật khẩu không đúng.')),
      http.post(authUrl('refresh'), refresh),
    )

    await fillLogin(open('/login'))

    expect(await screen.findByRole('alert')).toHaveTextContent('Email hoặc mật khẩu không đúng.')
    expect(refresh).not.toHaveBeenCalled()
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull()
  })
})

describe('Route và khôi phục phiên', () => {
  it('chuyển khách chưa đăng nhập về /login', async () => {
    open('/account')
    expect(await screen.findByRole('heading', { name: 'Đăng nhập' })).toBeVisible()
  })

  it.each([
    ['Customer', 'Bạn không có quyền truy cập'],
    ['Admin', 'Quản trị'],
  ])('áp dụng quyền %s sau khi tải lại trang', async (role, heading) => {
    localStorage.setItem(TOKEN_KEY, 'token-cached')
    const authorization = vi.fn()
    server.use(http.get(authUrl('me'), ({ request }) => {
      authorization(request.headers.get('authorization'))
      return success({ ...customer, role })
    }))

    open('/admin')

    expect(screen.getByRole('status')).toHaveTextContent('Đang kiểm tra phiên đăng nhập')
    expect(await screen.findByRole('heading', { name: heading })).toBeVisible()
    expect(authorization).toHaveBeenCalledExactlyOnceWith('Bearer token-cached')
  })

  it('refresh một lần khi /me trả 401 rồi gửi lại với token mới', async () => {
    localStorage.setItem(TOKEN_KEY, 'token-old')
    const authorization = vi.fn()
    const refresh = vi.fn(() => success({ accessToken: 'token-new' }))
    server.use(
      http.get(authUrl('me'), ({ request }) => {
        const header = request.headers.get('authorization')
        authorization(header)
        return header === 'Bearer token-new' ? success(customer) : unauthorized()
      }),
      http.post(authUrl('refresh'), refresh),
    )

    open('/account')

    expect(await screen.findByRole('heading', { name: 'Tài khoản của tôi' })).toBeVisible()
    expect(refresh).toHaveBeenCalledTimes(1)
    expect(authorization.mock.calls).toEqual([['Bearer token-old'], ['Bearer token-new']])
    expect(localStorage.getItem(TOKEN_KEY)).toBe('token-new')
  })

  it.each([false, true])('đăng xuất khi phiên không thể khôi phục (refresh thành công: %s)', async (refreshSucceeds) => {
    localStorage.setItem(TOKEN_KEY, 'token-old')
    const me = vi.fn(() => unauthorized())
    const refresh = vi.fn(() => refreshSucceeds ? success({ accessToken: 'token-new' }) : unauthorized())
    server.use(http.get(authUrl('me'), me), http.post(authUrl('refresh'), refresh))

    open('/forbidden')

    expect(await screen.findByRole('heading', { name: 'Đăng nhập' })).toBeVisible()
    expect(localStorage.getItem(TOKEN_KEY)).toBeNull()
    expect(refresh).toHaveBeenCalledTimes(1)
    expect(me).toHaveBeenCalledTimes(refreshSucceeds ? 2 : 1)
  })

  it('dùng chung một lượt refresh cho hai yêu cầu 401 đồng thời', async () => {
    localStorage.setItem(TOKEN_KEY, 'token-old')
    const expired = vi.fn()
    const refresh = vi.fn(async () => {
      await delay(40)
      return success({ accessToken: 'token-new' })
    })
    const authorization = vi.fn()
    server.use(
      http.post(authUrl('refresh'), refresh),
      http.get(authUrl('me'), ({ request }) => {
        const header = request.headers.get('authorization')
        authorization(header)
        return header === 'Bearer token-new' ? success(customer) : unauthorized()
      }),
    )
    const client = createAuthClient(vi.fn(), expired)
    const signal = new AbortController().signal

    await expect(Promise.all([client.me(signal), client.me(signal)])).resolves.toEqual([customer, customer])
    expect(refresh).toHaveBeenCalledTimes(1)
    expect(authorization.mock.calls.flat().sort()).toEqual([
      'Bearer token-new', 'Bearer token-new', 'Bearer token-old', 'Bearer token-old',
    ])
    expect(expired).not.toHaveBeenCalled()
  })
})
