import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import vueDevTools from 'vite-plugin-vue-devtools'
import { mdiSubset } from './build/mdiSubsetPlugin'

export default defineConfig({
  plugins: [
    vue(),
    vueDevTools(),
    // 圖示字型按需引入。名單在建置時掃描產生，不是版控裡的產生檔——
    // 新增前端圖示不需要任何額外動作；**新增導覽列模組後要重啟 dev server**，
    // 因為那些名稱來自後端種子檔，掃描只在 buildStart 跑一次。詳見外掛內的說明。
    mdiSubset(),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'https://localhost:7147',
        changeOrigin: true,
        secure: false,
      }
    }
  }
})