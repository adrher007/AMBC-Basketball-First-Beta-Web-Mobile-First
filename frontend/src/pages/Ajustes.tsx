import './Ajustes.module.css';
import Card from '../components/Card';

export default function Ajustes() {
  return (
    <div className="page">
      <h1>Ajustes</h1>
      <Card>
        <div className="ajuste-row">
          <span className="ajuste-label">Versión</span>
          <span className="ajuste-value">Beta 0.1</span>
        </div>
      </Card>
      <Card>
        <div className="ajuste-row">
          <span className="ajuste-label">Categoría</span>
          <span className="ajuste-value">Honor Masculino</span>
        </div>
      </Card>
      <p className="page-sub">
        Más ajustes disponibles próximamente.
      </p>
    </div>
  );
}
