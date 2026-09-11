import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import './App.css';
import { AuthProvider } from './context/AuthContext';
import { ThemeProvider } from './context/ThemeContext';
import Layout from './components/Layout';
import { RequireAuth, GuestOnly } from './components/guards';
import Login from './pages/Login';
import Home from './pages/Home';
import Teams from './pages/Teams';
import TeamDetallePage from './pages/TeamDetalle';
import Partidos from './pages/Partidos';
import Clasificacion from './pages/Clasificacion';
import Torneo from './pages/Torneo';
import Mas from './pages/Mas';
import Ajustes from './pages/Ajustes';
import Playoffs from './pages/Playoffs';
import AdminTemporada from './pages/admin/AdminTemporada';
import AdminEquipos from './pages/admin/AdminEquipos';
import AdminProgramarSemana from './pages/admin/AdminProgramarSemana';
import AdminResultados from './pages/admin/AdminResultados';
import AdminPlayoffs from './pages/admin/AdminPlayoffs';
import AdminEntrenadores from './pages/admin/AdminEntrenadores';
import AdminArbitros from './pages/admin/AdminArbitros';
import AdminPartidos from './pages/admin/AdminPartidos';
import EntrenadorPlantilla from './pages/entrenador/EntrenadorPlantilla';
import EntrenadorEquipo from './pages/entrenador/EntrenadorEquipo';
import ArbitroPlanillas from './pages/arbitro/ArbitroPlanillas';
import Placeholder from './pages/Placeholder';
import Notificaciones from './pages/Notificaciones';
import Chat from './pages/Chat';
import AdminAvisos from './pages/admin/AdminAvisos';
import AdminDashboard from './pages/admin/AdminDashboard';

const P = (t: string) => <Placeholder title={t} />;

export default function App() {
  return (
    <ThemeProvider>
      <AuthProvider>
        <BrowserRouter>
          <Routes>
            {/* Público */}
            <Route element={<GuestOnly />}>
              <Route path="/login" element={<Login />} />
            </Route>

            {/* Layout para todos (FAN sin login y usuarios logueados) */}
            <Route element={<Layout />}>
              <Route path="/" element={<Navigate to="/login" replace />} />
              <Route path="/inicio" element={<Home />} />
              <Route path="/partidos" element={<Partidos />} />
              <Route path="/partidos/:id" element={P('Detalle del partido')} />
              <Route path="/equipos" element={<Teams />} />
              <Route path="/equipos/:id" element={<TeamDetallePage />} />
              <Route path="/clasificacion" element={<Clasificacion />} />
              <Route path="/torneo" element={<Torneo />} />
              <Route path="/mas" element={<Mas />} />
              <Route path="/ajustes" element={<Ajustes />} />
              <Route path="/playoffs" element={<Playoffs />} />
              <Route path="/notificaciones" element={<Notificaciones />} />
              <Route path="/chat" element={<Chat />} />

              {/* ADMIN (requiere login + rol) */}
              <Route element={<RequireAuth roles={['ADMIN_AMBC']} />}>
                <Route path="/admin" element={<AdminDashboard />} />
                <Route path="/admin/temporada" element={<AdminTemporada />} />
                <Route path="/admin/partidos" element={<AdminPartidos />} />
                <Route path="/admin/partidos/programar" element={<AdminProgramarSemana />} />
                <Route path="/admin/resultados" element={<AdminResultados />} />
                <Route path="/admin/equipos" element={<AdminEquipos />} />
                <Route path="/admin/entrenadores" element={<AdminEntrenadores />} />
                <Route path="/admin/arbitros" element={<AdminArbitros />} />
                <Route path="/admin/playoffs" element={<AdminPlayoffs />} />
                <Route path="/admin/avisos" element={<AdminAvisos />} />
              </Route>

              {/* COACH (requiere login + rol) */}
              <Route element={<RequireAuth roles={['COACH']} />}>
                <Route path="/entrenador" element={<EntrenadorEquipo />} />
                <Route path="/entrenador/equipo" element={<EntrenadorEquipo />} />
                <Route path="/entrenador/plantilla" element={<EntrenadorPlantilla />} />
                <Route path="/entrenador/informacion" element={P('Información')} />
              </Route>

              {/* ARBITRO (requiere login + rol) */}
              <Route element={<RequireAuth roles={['ARBITRO']} />}>
                <Route path="/arbitro" element={<ArbitroPlanillas />} />
                <Route path="/arbitro/partidos" element={<ArbitroPlanillas />} />
                <Route path="/arbitro/planillas" element={<ArbitroPlanillas />} />
                <Route path="/arbitro/planillas/:id" element={<ArbitroPlanillas />} />
              </Route>
            </Route>

            {/* Fallback */}
            <Route path="*" element={<Navigate to="/" replace />} />
          </Routes>
        </BrowserRouter>
      </AuthProvider>
    </ThemeProvider>
  );
}
