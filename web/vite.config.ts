import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5101',
        changeOrigin: true,
        // Match Traefik's StripPrefix middleware in prod: API serves at
        // root paths (/health, /quizzes, ...) and the /api segment is
        // stripped before the request reaches it. Without this rewrite,
        // dev calls fail with 404 even though prod works.
        rewrite: (path) => path.replace(/^\/api/, ''),
      },
    },
  },
})
