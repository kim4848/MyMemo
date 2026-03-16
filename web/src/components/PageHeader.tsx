import type { ReactNode } from 'react';
import { Link } from 'react-router-dom';

interface Breadcrumb {
  label: string;
  to: string;
}

interface Props {
  title: string;
  subtitle?: string;
  breadcrumb?: Breadcrumb[];
  actions?: ReactNode;
}

export default function PageHeader({ title, subtitle, breadcrumb, actions }: Props) {
  return (
    <div className="mb-8 animate-[fadeInUp_0.3s_ease-out]">
      {breadcrumb && breadcrumb.length > 0 && (
        <nav className="mb-2 flex items-center gap-1.5 text-sm text-text-muted">
          {breadcrumb.map((item, i) => (
            <span key={item.to} className="flex items-center gap-1.5">
              {i > 0 && (
                <svg className="h-3.5 w-3.5" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
                  <path strokeLinecap="round" strokeLinejoin="round" d="m8.25 4.5 7.5 7.5-7.5 7.5" />
                </svg>
              )}
              <Link to={item.to} className="hover:text-accent transition-colors">
                {item.label}
              </Link>
            </span>
          ))}
        </nav>
      )}
      <div className="flex items-start justify-between gap-4">
        <div>
          <h1 className="font-heading text-2xl font-semibold text-text-primary">{title}</h1>
          {subtitle && (
            <p className="mt-1 text-sm text-text-secondary">{subtitle}</p>
          )}
        </div>
        {actions && <div className="flex-shrink-0">{actions}</div>}
      </div>
    </div>
  );
}
