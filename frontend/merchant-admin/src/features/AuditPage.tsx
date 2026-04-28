import { listAudit } from '../shared/audit'

export function AuditPage() {
  const rows = listAudit()
  
  return (
    <div>
      <div className="page-header">
        <h1 className="page-title">操作记录</h1>
        <p className="page-description">查看您的所有操作日志</p>
      </div>

      <div className="card">
        <div className="card-header">
          <h3 className="card-title">操作日志</h3>
          <div className="card-extra">
            <span className="text-muted">共 {rows.length} 条记录</span>
          </div>
        </div>
        <div className="card-body">
          {rows.length === 0 ? (
            <div className="empty-state">
              <div className="empty-icon">📝</div>
              <p className="empty-text">暂无操作记录</p>
            </div>
          ) : (
            <div className="table-container">
              <table>
                <thead>
                  <tr>
                    <th style={{ width: '200px' }}>时间</th>
                    <th style={{ width: '250px' }}>操作类型</th>
                    <th>详情</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((r, i) => (
                    <tr key={`${r.at}-${i}`}>
                      <td>
                        <span className="text-muted">{r.at}</span>
                      </td>
                      <td>
                        <span className="badge badge-primary">{r.action}</span>
                      </td>
                      <td>{r.detail}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}
