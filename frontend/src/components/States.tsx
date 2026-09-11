import './States.module.css';
import { Loader2, Inbox, AlertTriangle } from 'lucide-react';

export function LoadingState({ label = 'Cargando...' }: { label?: string }) {
  return (
    <div className="state-box">
      <Loader2 className="spin" size={24} />
      <span className="state-text">{label}</span>
    </div>
  );
}

export function EmptyState({ label }: { label: string }) {
  return (
    <div className="state-box">
      <Inbox size={28} />
      <span className="state-text">No hay {label}</span>
    </div>
  );
}

export function ErrorState({
  label,
  onRetry,
}: {
  label: string;
  onRetry?: () => void;
}) {
  return (
    <div className="state-box">
      <AlertTriangle size={28} />
      <span className="state-text">No pudimos cargar {label}</span>
      {onRetry && (
        <button className="state-retry" onClick={onRetry}>
          Reintentar
        </button>
      )}
    </div>
  );
}
