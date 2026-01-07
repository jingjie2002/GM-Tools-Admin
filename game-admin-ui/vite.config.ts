import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import path from 'path'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [vue()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src')
    }
  },
  server: {
    port: 5173,
    open: true,
    // 代理配置：将 /api 请求转发到后端
    proxy: {
      '/api': {
        target: 'http://localhost:5201',
        changeOrigin: true,
        secure: false,
        // 如果后端路由不带 /api 前缀，启用以下 rewrite
        // rewrite: (path) => path.replace(/^\/api/, '')
      }
    }
  }
})
