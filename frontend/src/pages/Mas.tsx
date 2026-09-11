import { useNavigate } from 'react-router-dom';
import './Mas.module.css';
import Card from '../components/Card';
import {
  MessageCircle,
  Settings,
  Sun,
  Moon,
  LogOut,
  ChevronRight,
} from 'lucide-react';
import { useTheme } from '../context/ThemeContext';
import { useAuth } from '../context/AuthContext';

export default function Mas() {
  const navigate = useNavigate();
  const { theme, toggle } = useTheme();
  const { user, logout } = useAuth();

  function handleLogout() {
    logout();
    navigate('/login');
  }

  return (
    <div className="page">
      <h1>Más</h1>

      <Card>
        <div className="mas-item" onClick={() => navigate('/chat')}>
          <MessageCircle size={22} />
          <span className="mas-label">Chat</span>
          <ChevronRight size={18} className="mas-chevron" />
        </div>
      </Card>

      <Card>
        <div className="mas-item" onClick={() => navigate('/ajustes')}>
          <Settings size={22} />
          <span className="mas-label">Ajustes</span>
          <ChevronRight size={18} className="mas-chevron" />
        </div>
      </Card>

      <Card>
        <div className="mas-item" onClick={toggle}>
          {theme === 'light' ? <Sun size={22} /> : <Moon size={22} />}
          <span className="mas-label">
            {theme === 'light' ? 'Tema claro' : 'Tema oscuro'}
          </span>
        </div>
      </Card>

      {user && (
        <Card>
          <div className="mas-item mas-danger" onClick={handleLogout}>
            <LogOut size={22} />
            <span className="mas-label">Cerrar sesión</span>
          </div>
        </Card>
      )}
    </div>
  );
}
