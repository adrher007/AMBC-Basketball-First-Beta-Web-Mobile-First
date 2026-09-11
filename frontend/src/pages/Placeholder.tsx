import './Placeholder.module.css';
import { EmptyState } from '../components/States';

export default function Placeholder({ title }: { title: string }) {
  return (
    <div className="page">
      <h1>{title}</h1>
      <p className="page-sub">Módulo en construcción.</p>
      <EmptyState label={`contenido de ${title.toLowerCase()}`} />
    </div>
  );
}
