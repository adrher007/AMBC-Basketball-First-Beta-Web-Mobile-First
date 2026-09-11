import './Chat.module.css';
import { MessageCircle, Lock } from 'lucide-react';

export default function Chat() {
  return (
    <div className="page">
      <h1>Chat</h1>
      <div className="chat-placeholder">
        <div className="chat-icon">
          <MessageCircle size={36} />
        </div>
        <h2>Chat próximamente</h2>
        <p className="chat-sub">
          Esta función aún no está disponible. Podrás conversar en tiempo real
          cuando la habilitemos.
        </p>
        <div className="chat-disabled">
          <Lock size={14} />
          <span>Sin funcionalidad en esta versión</span>
        </div>
      </div>
    </div>
  );
}
