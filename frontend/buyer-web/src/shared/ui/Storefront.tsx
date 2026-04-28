import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'

export function PageHeader({ title, subtitle, extra }: { title: string; subtitle?: string; extra?: ReactNode }) {
  return (
    <div className="page-header">
      <div>
        <h2>{title}</h2>
        {subtitle ? <p className="muted">{subtitle}</p> : null}
      </div>
      {extra ? <div className="page-header-extra">{extra}</div> : null}
    </div>
  )
}

export function SurfaceCard({ children, className = '' }: { children: ReactNode; className?: string }) {
  return <article className={`surface-card ${className}`.trim()}>{children}</article>
}

export function PriceText({ value }: { value: number }) {
  return (
    <p className="price-text">
      <span className="price-symbol">￥</span>
      {value.toFixed(2)}
    </p>
  )
}

export function EmptyState({
  title,
  description,
  actionText,
  actionTo,
}: {
  title: string
  description?: string
  actionText?: string
  actionTo?: string
}) {
  return (
    <div className="empty-state surface-card">
      <h3>{title}</h3>
      {description ? <p className="muted">{description}</p> : null}
      {actionText && actionTo ? (
        <Link className="btn btn-primary" to={actionTo}>
          {actionText}
        </Link>
      ) : null}
    </div>
  )
}

export function StatusPill({ text }: { text: string }) {
  return <span className="status-pill">{text}</span>
}

