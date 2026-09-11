import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import './Login.module.css';
import Input from '../components/Input';
import Button from '../components/Button';
import { useAuth } from '../context/AuthContext';
import { destinoPorRol } from '../components/guards';
import { ShieldCheck, UserRoundPlus, ClipboardList, Users } from 'lucide-react';

type Rol = 'ADMIN_AMBC' | 'COACH' | 'ARBITRO' | 'FAN';

const ROLES: { key: Rol; label: string; icon: React.ReactNode }[] = [
  { key: 'ADMIN_AMBC', label: 'Administrador', icon: <ShieldCheck size={22} /> },
  { key: 'COACH', label: 'Entrenador', icon: <UserRoundPlus size={22} /> },
  { key: 'ARBITRO', label: 'Árbitro', icon: <ClipboardList size={22} /> },
  { key: 'FAN', label: 'Aficionado', icon: <Users size={22} /> },
];

export default function Login() {
  const { login } = useAuth();
  const navigate = useNavigate();

  const [rol, setRol] = useState<Rol | null>(null);
  const [username, setUsername] = useState('');
  const [password, setPassword] = useState('');
  const [remember, setRemember] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  function selectRol(r: Rol) {
    setRol(r);
    setError('');
    if (r === 'FAN') {
      navigate('/inicio', { replace: true });
    }
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      const auth = await login(username, password, remember);
      navigate(destinoPorRol(auth.rol), { replace: true });
    } catch (err: any) {
      const status = err?.response?.status;
      setError(
        status === 401
          ? 'Credenciales inválidas o usuario inactivo.'
          : 'No pudimos conectar con el servidor.',
      );
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="login-wrap">
      {/* Logo escudo (PNG subido por el admin en public/logo.png) */}
      <div className="login-logo" aria-label="Escudo AMBC">
        <img
          src="/logo.png"
          alt="Escudo AMBC"
          className="logo-img"
          onError={(e) => {
            // Si no hay logo.png, ocultar la imagen y dejar el espacio vacío
            (e.target as HTMLImageElement).style.display = 'none';
          }}
        />
      </div>

      {/* Subtítulo */}
      <h1 className="login-title">
        Asociacion Municipal de Baloncesto
        <span>Cochabamba</span>
      </h1>

      {rol === null ? (
        <>
          {/* Lista iOS de roles */}
          <div className="role-list">
            {ROLES.map((r) => (
              <button
                key={r.key}
                type="button"
                className="role-btn"
                onClick={() => selectRol(r.key)}
              >
                <span className="role-icon">{r.icon}</span>
                <span className="role-label">{r.label}</span>
              </button>
            ))}
          </div>
        </>
      ) : (
        <form className="login-form" onSubmit={handleSubmit}>
          <button type="button" className="role-pill" onClick={() => setRol(null)}>
            ← Cambiar rol
          </button>

          <Input
            label="Usuario"
            value={username}
            onChange={setUsername}
            placeholder="Nombre de usuario"
            autoFocus
          />
          <Input
            label="Contraseña"
            type="password"
            value={password}
            onChange={setPassword}
            placeholder="••••••••"
          />

          <label className="remember">
            <input
              type="checkbox"
              checked={remember}
              onChange={(e) => setRemember(e.target.checked)}
            />
            <span>Recuérdame</span>
          </label>

          {error && <div className="login-error">{error}</div>}

          <Button type="submit" loading={loading} fullWidth>
            Entrar
          </Button>
        </form>
      )}
    </div>
  );
}
