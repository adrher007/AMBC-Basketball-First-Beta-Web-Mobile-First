import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  css: {
    modules: {
      // Los componentes usan clases literales (className="card", "btn", etc.)
      // con import './X.module.css'. Desactivamos el scope de CSS Modules
      // para que esas clases se inyecten de forma global y matcheen el HTML.
      generateScopedName: (name) => name,
    },
  },
  server: {
    host: '0.0.0.0',
    port: 5173,
    proxy: {
      '/api': {
        // El backend corre en la misma máquina que el dev server, siempre.
        // Usar localhost evita hardcodear la IP de red (que cambia en casa vs uni).
        target: 'http://localhost:5083',
        changeOrigin: true,
      },
      // Imágenes subidas (logos, planillas) — antes se usaba localhost:5083
      // hardcodeado, que falla al abrir la app desde el celular.
      '/uploads': {
        target: 'http://localhost:5083',
        changeOrigin: true,
      },
    },
  },
})