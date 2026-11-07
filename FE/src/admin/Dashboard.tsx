import { useState } from 'react'
import { ResponsiveContainer, LineChart, Line, XAxis, YAxis, Tooltip, CartesianGrid } from 'recharts'
import { useApiResource } from '../shared/useApiResource'
import { ResourceState } from '../shared/ResourceState'
import { formatVND } from '../catalog/formatVND'
import { OrderStatus } from '../orders/OrderStatus'

interface Overview { totalRevenue: number; newCustomers: number; lowStockProducts: number; ordersByStatus: { status: string; count: number }[] }
export function Dashboard() {
  const [from, setFrom] = useState('')
  const [to, setTo] = useState('')
  const [groupBy, setGroupBy] = useState('day')
  const invalid = Boolean(from && to && from > to)
  const query = new URLSearchParams({ ...(from ? { from } : {}), ...(to ? { to } : {}) }).toString()
  const overview = useApiResource<Overview>(invalid ? null : '/admin/stats/overview?' + query)
  const revenue = useApiResource<{ period: string; revenue: number }[]>(invalid ? null : '/admin/stats/revenue?' + query + '&groupBy=' + groupBy)
  const top = useApiResource<{ productId: number; name: string; quantitySold: number }[]>('/admin/stats/top-products?limit=10')
  return <>
    <h2 className="text-2xl font-bold">Tổng quan kinh doanh</h2>
    <div className="flex flex-wrap gap-3">
      <label>Từ ngày<input type="date" value={from} onChange={e => setFrom(e.target.value)} className="block rounded border p-2" /></label>
      <label>Đến ngày<input type="date" value={to} onChange={e => setTo(e.target.value)} className="block rounded border p-2" /></label>
      <label>Nhóm theo<select value={groupBy} onChange={e => setGroupBy(e.target.value)} className="block rounded border p-2"><option value="day">Ngày</option><option value="month">Tháng</option></select></label>
    </div>
    {invalid && <p role="alert">Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.</p>}
    <ResourceState {...overview} />
    {overview.data && <>
      <div className="grid gap-3 sm:grid-cols-3">{[
        ['Doanh thu', formatVND(overview.data.totalRevenue)], ['Khách mới', overview.data.newCustomers], ['Sắp hết hàng', overview.data.lowStockProducts],
      ].map(([name, value]) => <div key={name} className="rounded-xl border bg-white p-4"><p>{name}</p><strong className="text-2xl">{value}</strong></div>)}</div>
      <div className="flex flex-wrap gap-3">{overview.data.ordersByStatus.map(x => <p key={x.status}><OrderStatus status={x.status} /> {x.count} đơn</p>)}</div>
    </>}
    <section className="rounded-xl border bg-white p-4">
      <h3 className="font-bold">Doanh thu theo thời gian</h3><ResourceState {...revenue} />
      {revenue.data && (revenue.data.length ? <div aria-label="Biểu đồ doanh thu">
        <ResponsiveContainer width="100%" height={260} initialDimension={{ width: 600, height: 260 }}>
          <LineChart data={revenue.data}><CartesianGrid strokeDasharray="3 3" /><XAxis dataKey="period" /><YAxis width={80} />
            <Tooltip /><Line type="monotone" dataKey="revenue" name="Doanh thu" stroke="#0f766e" isAnimationActive={false} /></LineChart>
        </ResponsiveContainer>
        <details><summary>Xem dữ liệu doanh thu</summary><table className="w-full"><thead><tr><th>Ngày/kỳ</th><th>Doanh thu</th></tr></thead><tbody>
          {revenue.data.map(x => <tr key={x.period}><td>{x.period}</td><td>{formatVND(x.revenue)}</td></tr>)}
        </tbody></table></details>
      </div> : <p>Chưa có doanh thu trong khoảng ngày này.</p>)}
    </section>
    <section><h3 className="font-bold">Sản phẩm bán chạy · toàn thời gian</h3><ResourceState {...top} />
      {top.data && (top.data.length ? <table className="w-full"><thead><tr><th>Sản phẩm</th><th>Số lượng bán</th></tr></thead><tbody>
        {top.data.map(x => <tr key={x.productId}><td>{x.name}</td><td className="text-center">{x.quantitySold}</td></tr>)}
      </tbody></table> : <p>Chưa có sản phẩm bán chạy.</p>)}
    </section>
  </>
}

