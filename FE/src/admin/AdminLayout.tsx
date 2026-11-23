import { NavLink, Outlet } from 'react-router-dom'
export function AdminLayout() {
  return <div className="grid gap-6 lg:grid-cols-[170px_minmax(0,1fr)]">
    <aside><h1 className="mb-4 text-2xl font-bold">Quản trị</h1>
      <nav aria-label="Quản trị" className="flex flex-wrap gap-2 lg:flex-col">
        {[['', 'Dashboard'], ['/products', 'Sản phẩm'], ['/inventory', 'Tồn kho'], ['/orders', 'Đơn hàng'], ['/coupons', 'Mã giảm giá'], ['/reviews', 'Đánh giá']].map(([path, name]) =>
          <NavLink end key={path} to={'/admin' + path} className={({ isActive }) => 'rounded px-3 py-2 ' + (isActive ? 'bg-teal-800 text-white' : 'bg-white border')}>{name}</NavLink>)}
      </nav>
    </aside><div className="min-w-0 space-y-5"><Outlet /></div>
  </div>
}

