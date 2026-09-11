import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import './Home.module.css';
import Card from '../components/Card';
import Badge from '../components/Badge';
import { LoadingState } from '../components/States';
import { getSeasons } from '../lib/catalog';
import { ChevronRight } from 'lucide-react';
import type { Season } from '../lib/types';

export default function Home() {
  const [active, setActive] = useState<Season | null>(null);
  const [loading, setLoading] = useState(true);
  const navigate = useNavigate();

  useEffect(() => {
    getSeasons()
      .then((list) => setActive(list.find((s) => s.estado === 'ACTIVE') ?? null))
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="page">
      <h1>Inicio</h1>
      {loading ? (
        <LoadingState />
      ) : active ? (
        <Card onClick={() => navigate('/partidos')} className="home-season-card">
          <div className="home-season">
            <div>
              <h2>{active.nombre}</h2>
              <div className="home-sub">
                {active.fechaInicio} → {active.fechaFin}
              </div>
            </div>
            <div className="home-right">
              <Badge color="success">EN CURSO</Badge>
              <ChevronRight size={20} className="home-chevron" />
            </div>
          </div>
        </Card>
      ) : (
        <Card>
          <div className="home-season">
            <h2>Sin temporada activa</h2>
            <div className="home-sub">
              El administrador aún no ha iniciado una temporada.
            </div>
          </div>
        </Card>
      )}
    </div>
  );
}
