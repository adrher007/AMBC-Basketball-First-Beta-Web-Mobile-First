import './Badge.module.css';

export default function Badge({
  children,
  color = 'default',
}: {
  children: React.ReactNode;
  color?: 'default' | 'accent' | 'success' | 'danger';
}) {
  return <span className={`badge badge-${color}`}>{children}</span>;
}
