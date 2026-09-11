import './Button.module.css';
import { Loader2 } from 'lucide-react';

type Variant = 'primary' | 'secondary' | 'ghost' | 'danger';

export default function Button({
  children,
  variant = 'primary',
  onClick,
  type = 'button',
  disabled = false,
  loading = false,
  fullWidth = false,
}: {
  children: React.ReactNode;
  variant?: Variant;
  onClick?: () => void;
  type?: 'button' | 'submit';
  disabled?: boolean;
  loading?: boolean;
  fullWidth?: boolean;
}) {
  return (
    <button
      type={type}
      className={`btn btn-${variant} ${fullWidth ? 'btn-full' : ''}`}
      onClick={onClick}
      disabled={disabled || loading}
    >
      {loading && <Loader2 className="spin" size={16} />}
      {children}
    </button>
  );
}
