import { useState } from 'react';
import './Torneo.module.css';
import Clasificacion from './Clasificacion';
import Playoffs from './Playoffs';

export default function Torneo() {
  const [tab, setTab] = useState<'clasificacion' | 'playoffs'>('clasificacion');

  return (
    <div className="torneo">
      <div className="torneo-tabs" role="tablist">
        <button
          className={`torneo-tab ${tab === 'clasificacion' ? 'active' : ''}`}
          onClick={() => setTab('clasificacion')}
        >
          Clasificación
        </button>
        <button
          className={`torneo-tab ${tab === 'playoffs' ? 'active' : ''}`}
          onClick={() => setTab('playoffs')}
        >
          Playoffs
        </button>
      </div>
      {tab === 'clasificacion' ? <Clasificacion /> : <Playoffs />}
    </div>
  );
}
