import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5174,
    // 开发模式代理 ipc.js 和 RPC 端点到 lybox-mock
    proxy: {
      '/__lybox': { target: 'http://localhost:5173', changeOrigin: true },
      '/__rpc': { target: 'http://localhost:5173', changeOrigin: true },
      '/sse': { target: 'http://localhost:5173', changeOrigin: true, ws: false },
      '/__emit': { target: 'http://localhost:5173', changeOrigin: true },
      '/__channel': { target: 'http://localhost:5173', changeOrigin: true },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
  },
});
