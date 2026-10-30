import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: '/app/',
  build: {
    outDir: '../LabWeb/wwwroot/app',
    emptyOutDir: true
  },
  server: {
    proxy: { '/api': 'http://localhost:5080' }
  }
});
