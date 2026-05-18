import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')

  // When running under Aspire, the identity server URL is injected as a process env var.
  // Vite only exposes VITE_* vars to client code, so we bridge it via define.
  const authority =
    process.env['services__SampleIdentityWeb__https__0'] ??
    env['VITE_AUTHORITY'] ??
    'https://localhost:5003'

  return {
    plugins: [react()],
    define: {
      'import.meta.env.VITE_AUTHORITY': JSON.stringify(authority),
    },
    server: {
      port: parseInt(process.env['PORT'] ?? '5173'),
      strictPort: true,
      proxy: {
        '/api': {
          target: process.env['services__SampleApi__https__0'] ?? 'https://localhost:7001',
          changeOrigin: true,
          secure: false,
          rewrite: (path: string) => path.replace(/^\/api/, ''),
        },
      },
    },
  }
})
